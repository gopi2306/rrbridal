using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Sync;

namespace RRBridal.StoreBilling.App.Services.Store;

public sealed class CustomerBillingReportService
{
    public const int MaxRows = 10_000;
    private static readonly TimeSpan BusinessOffset = TimeSpan.FromHours(5.5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMongoDatabase _db;
    private readonly HttpClient _centralApi;
    private readonly StoreContext _storeContext;
    private CentralOnlineModeService? _centralMode;

    public CustomerBillingReportService(
        IMongoDatabase localDb,
        HttpClient centralApi,
        StoreContext storeContext)
    {
        _db = localDb;
        _centralApi = centralApi;
        _storeContext = storeContext;
    }

    public void ConfigureOnline(CentralOnlineModeService centralMode) => _centralMode = centralMode;

    public Task<CustomerBillingReportResponse> LoadAsync(
        CustomerBillingReportQuery query,
        CancellationToken ct = default)
    {
        Validate(query);
        return _centralMode?.IsOnlineMode == true
            ? LoadOnlineAsync(query, ct)
            : LoadOfflineAsync(query, ct);
    }

    public async Task SaveExportAsync(
        string filePath,
        CustomerBillingReportQuery query,
        CancellationToken ct = default)
    {
        Validate(query);
        if (_centralMode?.IsOnlineMode == true)
        {
            using var response = await _centralApi.GetAsync(BuildUri(query, export: true), ct).ConfigureAwait(false);
            var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw BuildCentralError("download", response.StatusCode, System.Text.Encoding.UTF8.GetString(bytes));
            await System.IO.File.WriteAllBytesAsync(filePath, bytes, ct).ConfigureAwait(false);
            return;
        }

        var report = await LoadOfflineAsync(query, ct).ConfigureAwait(false);
        CustomerBillingReportExcelExporter.ExportToFile(filePath, report);
    }

    private async Task<CustomerBillingReportResponse> LoadOnlineAsync(
        CustomerBillingReportQuery query,
        CancellationToken ct)
    {
        using var response = await _centralApi.GetAsync(BuildUri(query, export: false), ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw BuildCentralError("load", response.StatusCode, body);
        return JsonSerializer.Deserialize<CustomerBillingReportResponse>(body, JsonOptions)
               ?? throw new InvalidOperationException("Central customer billing report returned an empty response.");
    }

    private async Task<CustomerBillingReportResponse> LoadOfflineAsync(
        CustomerBillingReportQuery query,
        CancellationToken ct)
    {
        var storeCode = EffectiveStoreCode(query);
        var billDocs = await _db.GetCollection<BsonDocument>("store_bills")
            .Find(Builders<BsonDocument>.Filter.Eq("storeId", storeCode))
            .ToListAsync(ct).ConfigureAwait(false);

        var matching = billDocs
            .Where(IsPosted)
            .Select(doc => (Doc: doc, Occurred: ReadOccurredAt(doc)))
            .Where(row => row.Occurred.HasValue && IsInBusinessRange(row.Occurred.Value, query.From, query.To))
            .Where(row => MatchesBillFilters(row.Doc, query))
            .OrderByDescending(row => row.Occurred)
            .ThenByDescending(row => ReadString(row.Doc, "billNo"), StringComparer.Ordinal)
            .ToList();

        var total = matching.Count;
        var limit = Math.Clamp(query.Limit, 1, MaxRows);
        var selected = matching.Take(limit).ToList();
        var billNos = selected
            .Select(row => ReadString(row.Doc, "billNo"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);

        var returnDocs = billNos.Count == 0
            ? []
            : await _db.GetCollection<BsonDocument>("store_sale_returns")
                .Find(Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Eq("storeId", storeCode),
                    Builders<BsonDocument>.Filter.In("originalBillNo", billNos)))
                .Sort(Builders<BsonDocument>.Sort.Ascending("createdAtUtc"))
                .ToListAsync(ct).ConfigureAwait(false);

        var returnsByBill = returnDocs
            .Where(IsPosted)
            .GroupBy(doc => ReadString(doc, "originalBillNo"), StringComparer.Ordinal)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        var (data, totals) = Aggregate(selected, returnsByBill);
        return new CustomerBillingReportResponse
        {
            Period = new CustomerBillingReportPeriod
            {
                From = FormatYmd(query.From),
                To = FormatYmd(query.To),
                Timezone = "Asia/Kolkata",
                StoreCode = storeCode,
                StoreName = string.IsNullOrWhiteSpace(query.StoreName) ? storeCode : query.StoreName.Trim(),
                PosCounter = TrimOrNull(query.PosCounter),
            },
            Filters = new CustomerBillingReportFilters
            {
                CustomerSearch = TrimOrNull(query.CustomerSearch),
                CustomerCode = TrimOrNull(query.CustomerCode),
                CustomerPhone = TrimOrNull(query.CustomerPhone),
            },
            Limit = limit,
            Truncated = total > limit,
            Total = total,
            Totals = totals,
            Data = data,
        };
    }

    internal static (IReadOnlyList<CustomerBillingCustomerRow> Data, CustomerBillingTotals Totals) Aggregate(
        IEnumerable<(BsonDocument Doc, DateTimeOffset? Occurred)> invoices,
        IReadOnlyDictionary<string, List<BsonDocument>> returnsByBill)
    {
        var rows = new Dictionary<string, CustomerBillingCustomerRow>(StringComparer.Ordinal);
        foreach (var (doc, occurred) in invoices)
        {
            if (!IsPosted(doc))
                continue;

            var code = ReadString(doc, "customerCode");
            var name = ReadString(doc, "customerName");
            var phone = ReadString(doc, "customerPhone");
            var key = CustomerKey(code, name, phone);
            if (key == "walk-in" && string.IsNullOrWhiteSpace(name))
                name = "Walk-in Customer";

            var billNo = ReadString(doc, "billNo");
            returnsByBill.TryGetValue(billNo, out var returnDocs);
            var returns = (returnDocs ?? [])
                .Where(IsPosted)
                .Select(MapReturn)
                .ToList();
            var returned = Round(returns.Sum(row => row.Amount));
            var payments = ParsePayments(doc);
            var gross = ParseBillAmount(doc, payments);
            var bill = new CustomerBillingBillDetail
            {
                BillNo = billNo,
                BillDate = ReadString(doc, "billDate") is { Length: > 0 } billDate
                    ? billDate
                    : occurred.HasValue ? FormatBusinessYmd(occurred.Value) : "",
                PosCounter = ReadString(doc, "posCounter"),
                CustomerCode = code,
                CustomerName = name,
                CustomerPhone = phone,
                Qty = Round(SumLineQty(doc, "lines", returnLines: false)),
                GrossAmount = gross,
                ReturnAmount = returned,
                NetAmount = Round(gross - returned),
                Payments = payments,
                Returns = returns,
                Lines = MapBillLines(doc),
            };

            if (!rows.TryGetValue(key, out var row))
            {
                row = new CustomerBillingCustomerRow
                {
                    CustomerKey = key,
                    CustomerCode = code,
                    CustomerName = name,
                    CustomerPhone = phone,
                };
                rows.Add(key, row);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(row.CustomerCode)) row.CustomerCode = code;
                if (string.IsNullOrWhiteSpace(row.CustomerName)) row.CustomerName = name;
                if (string.IsNullOrWhiteSpace(row.CustomerPhone)) row.CustomerPhone = phone;
            }

            row.BillCount++;
            row.Qty = Round(row.Qty + bill.Qty);
            row.GrossAmount = Round(row.GrossAmount + bill.GrossAmount);
            row.ReturnAmount = Round(row.ReturnAmount + bill.ReturnAmount);
            row.NetAmount = Round(row.NetAmount + bill.NetAmount);
            AddPayments(row.Payments, bill.Payments);
            row.Bills.Add(bill);
        }

        foreach (var row in rows.Values)
        {
            row.Bills.Sort((left, right) =>
            {
                var date = string.Compare(right.BillDate, left.BillDate, StringComparison.Ordinal);
                return date != 0 ? date : string.Compare(right.BillNo, left.BillNo, StringComparison.Ordinal);
            });
        }

        var data = rows.Values
            .OrderByDescending(row => row.NetAmount)
            .ThenBy(row => row.CustomerName, StringComparer.Ordinal)
            .ThenBy(row => row.CustomerKey, StringComparer.Ordinal)
            .ToList();
        var totals = new CustomerBillingTotals { CustomerCount = data.Count };
        foreach (var row in data)
        {
            totals.BillCount += row.BillCount;
            totals.Qty = Round(totals.Qty + row.Qty);
            totals.GrossAmount = Round(totals.GrossAmount + row.GrossAmount);
            totals.ReturnAmount = Round(totals.ReturnAmount + row.ReturnAmount);
            totals.NetAmount = Round(totals.NetAmount + row.NetAmount);
            AddPayments(totals.Payments, row.Payments);
        }
        return (data, totals);
    }

    private string BuildUri(CustomerBillingReportQuery query, bool export)
    {
        var values = new List<(string Key, string? Value)>
        {
            ("storeCode", EffectiveStoreCode(query)),
            ("from", FormatYmd(query.From)),
            ("to", FormatYmd(query.To)),
            ("customerSearch", TrimOrNull(query.CustomerSearch)),
            ("customerCode", TrimOrNull(query.CustomerCode)),
            ("customerPhone", TrimOrNull(query.CustomerPhone)),
            ("posCounter", TrimOrNull(query.PosCounter)),
            ("limit", Math.Clamp(query.Limit, 1, MaxRows).ToString(CultureInfo.InvariantCulture)),
        };
        var qs = string.Join("&", values
            .Where(pair => pair.Value != null)
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}"));
        return $"/api/reports/customer-billing{(export ? "/export" : "")}?{qs}";
    }

    private string EffectiveStoreCode(CustomerBillingReportQuery query) =>
        string.IsNullOrWhiteSpace(query.StoreCode) ? _storeContext.StoreId : query.StoreCode.Trim();

    private static bool MatchesBillFilters(BsonDocument doc, CustomerBillingReportQuery query)
    {
        if (TrimOrNull(query.PosCounter) is { } counter
            && !string.Equals(ReadString(doc, "posCounter"), counter, StringComparison.OrdinalIgnoreCase))
            return false;
        if (TrimOrNull(query.CustomerCode) is { } code
            && !string.Equals(ReadString(doc, "customerCode"), code, StringComparison.OrdinalIgnoreCase))
            return false;
        if (TrimOrNull(query.CustomerPhone) is { } phone
            && !PhoneContains(ReadString(doc, "customerPhone"), phone))
            return false;
        if (TrimOrNull(query.CustomerSearch) is not { } search)
            return true;
        return ReadString(doc, "customerCode").Contains(search, StringComparison.OrdinalIgnoreCase)
               || ReadString(doc, "customerName").Contains(search, StringComparison.OrdinalIgnoreCase)
               || PhoneContains(ReadString(doc, "customerPhone"), search);
    }

    private static CustomerBillingReturnDetail MapReturn(BsonDocument doc)
    {
        var occurred = ReadOccurredAt(doc);
        var returnTotal = ReadDecimal(doc, "returnTotal");
        return new CustomerBillingReturnDetail
        {
            ReturnNo = ReadString(doc, "returnNo"),
            ReturnDate = FirstNonEmpty(ReadString(doc, "returnDate"), ReadString(doc, "businessDate"),
                occurred.HasValue ? FormatBusinessYmd(occurred.Value) : ""),
            Kind = FirstNonEmpty(ReadString(doc, "kind"), "return"),
            ReturnMode = ReadString(doc, "returnMode"),
            CreditNoteNo = ReadString(doc, "creditNoteNo"),
            Qty = Round(SumLineQty(doc, "returnLines", returnLines: true)),
            Amount = returnTotal > 0 ? returnTotal : Round(SumReturnLineAmounts(doc)),
        };
    }

    private static List<CustomerBillingLineDetail> MapBillLines(BsonDocument doc)
    {
        var result = new List<CustomerBillingLineDetail>();
        var lineNo = 0;
        foreach (var line in ReadLines(doc, "lines"))
        {
            var qty = ReadDecimal(line, "qty");
            if (qty <= 0) continue;
            lineNo++;
            result.Add(new CustomerBillingLineDetail
            {
                LineNo = lineNo,
                Sku = FirstNonEmpty(ReadString(line, "sku"), ReadString(line, "productCode"), "UNKNOWN"),
                Description = FirstNonEmpty(ReadString(line, "description"), ReadString(line, "sku"), "Product"),
                Hsn = ReadString(line, "hsn"),
                Qty = Round(qty),
                Rate = Round(ReadDecimal(line, "rate")),
                Amount = Round(LineAmount(line, qty)),
                DiscountAmount = Round(
                    FirstPositive(ReadDecimal(line, "discountAmount"), ReadDecimal(line, "itemDiscountAmount"))
                    + ReadDecimal(line, "cashDiscountAmount")
                    + ReadDecimal(line, "schemeDiscountAmount")),
                TaxAmount = Round(FirstPositive(
                    ReadDecimal(line, "taxAmount"),
                    ReadDecimal(line, "revisedTaxAmount"),
                    ReadDecimal(line, "cgstAmount") + ReadDecimal(line, "sgstAmount") + ReadDecimal(line, "igstAmount"))),
            });
        }
        return result;
    }

    private static decimal LineAmount(BsonDocument line, decimal qty)
    {
        var amount = ReadDecimal(line, "amount");
        if (amount > 0) return amount;
        var revised = ReadDecimal(line, "revisedAmount");
        if (revised > 0) return revised + Math.Max(0, ReadDecimal(line, "revisedTaxAmount"));
        var rate = ReadDecimal(line, "rate");
        return rate > 0 && qty > 0 ? rate * qty : 0;
    }

    private static decimal SumReturnLineAmounts(BsonDocument doc)
    {
        var lines = ReadLines(doc, "returnLines");
        if (lines.Count == 0)
            lines = ReadLines(doc, "lines");
        decimal total = 0;
        foreach (var line in lines)
        {
            var qty = ReadDecimal(line, "returnQty");
            if (qty <= 0) qty = ReadDecimal(line, "qty");
            if (qty <= 0) continue;
            var amount = FirstPositive(
                ReadDecimal(line, "revisedInclusiveAmount"),
                ReadDecimal(line, "lineTotal"),
                ReadDecimal(line, "amount"));
            if (amount <= 0)
            {
                var taxable = FirstPositive(ReadDecimal(line, "revisedAmount"), ReadDecimal(line, "amount"));
                var tax = FirstPositive(ReadDecimal(line, "revisedTaxAmount"), ReadDecimal(line, "taxAmount"),
                    ReadDecimal(line, "taxAmt"));
                if (tax <= 0)
                    tax = FirstPositive(ReadDecimal(line, "cgstAmount"), ReadDecimal(line, "cgstAmt"))
                          + FirstPositive(ReadDecimal(line, "sgstAmount"), ReadDecimal(line, "sgstAmt"))
                          + FirstPositive(ReadDecimal(line, "igstAmount"), ReadDecimal(line, "igstAmt"));
                if (taxable > 0) amount = taxable + tax;
            }
            if (amount <= 0)
                amount = ReadDecimal(line, "rate") * qty;
            total += Round(amount);
        }
        return total;
    }

    private static CustomerBillingPaymentSplits ParsePayments(BsonDocument doc)
    {
        var totals = new CustomerBillingPaymentSplits();
        if (IsOnlineCod(doc) && ReadNestedString(doc, "onlineCod", "status")
                .Equals("pending", StringComparison.OrdinalIgnoreCase))
            return totals;

        var payments = ReadLines(doc, "payments");
        foreach (var payment in payments)
            AddPayment(totals, ClassifyProvider(FirstNonEmpty(
                ReadString(payment, "provider"), ReadString(payment, "Provider"), "Other")),
                ReadDecimal(payment, "amount"));

        var payable = ReadDecimal(doc, "payable");
        var creditApplied = ReadDecimal(doc, "creditApplied");
        var beforeCredit = ReadDecimal(doc, "payableBeforeCredit");
        if (beforeCredit <= 0 && (creditApplied > 0 || payable > 0))
            beforeCredit = Round(payable + creditApplied);
        var target = beforeCredit > 0 ? beforeCredit : payable;

        if (payments.Count == 0 && IsOnlineCod(doc)
            && ReadNestedString(doc, "onlineCod", "status").Equals("received", StringComparison.OrdinalIgnoreCase))
        {
            var amount = ReadNestedDecimal(doc, "onlineCod", "amount");
            if (amount <= 0) amount = payable;
            var mode = ClassifyProvider(FirstNonEmpty(
                ReadNestedString(doc, "onlineCod", "receivedPaymentMode"),
                ReadString(doc, "paymentMode")));
            AddPayment(totals, mode, amount);
        }
        else
        {
            if (creditApplied > totals.CreditNote)
                totals.CreditNote = Round(totals.CreditNote + creditApplied - totals.CreditNote);
            var gap = Round(target - SumPayments(totals));
            if (gap > (payments.Count == 0 ? 0m : 0.01m) && !IsCreditBillingOpen(doc))
            {
                var mode = ClassifyProvider(ReadString(doc, "paymentMode"));
                if (mode == "Other") mode = "Cash";
                AddPayment(totals, mode, gap);
            }
        }
        RoundPayments(totals);
        return totals;
    }

    private static decimal ParseBillAmount(BsonDocument doc, CustomerBillingPaymentSplits payments)
    {
        var payable = ReadDecimal(doc, "payable");
        var beforeCredit = ReadDecimal(doc, "payableBeforeCredit");
        if (beforeCredit <= 0)
        {
            var credit = ReadDecimal(doc, "creditApplied");
            if (credit > 0 || payable > 0) beforeCredit = Round(payable + credit);
        }
        var paymentSum = SumPayments(payments);
        if (beforeCredit > 0) return beforeCredit;
        if (paymentSum > 0 && payable <= 0) return paymentSum;
        return payable > 0 ? payable : paymentSum;
    }

    private static void AddPayment(CustomerBillingPaymentSplits totals, string mode, decimal amount)
    {
        if (amount <= 0) return;
        switch (mode)
        {
            case "Cash": totals.Cash += amount; break;
            case "Card": totals.Card += amount; break;
            case "UPI": totals.Upi += amount; break;
            case "Credit": totals.CreditNote += amount; break;
        }
    }

    private static string ClassifyProvider(string provider)
    {
        var value = new string(provider.Where(ch => !char.IsWhiteSpace(ch)).ToArray()).ToLowerInvariant();
        if (value == "cash") return "Cash";
        if (value == "pinelabs" || value.Contains("pine") || value == "card") return "Card";
        if (value == "razorpay" || value.Contains("razor") || value == "upi") return "UPI";
        if (value is "creditnote" or "credit_note") return "Credit";
        return "Other";
    }

    private static bool IsCreditBillingOpen(BsonDocument doc)
    {
        if (!doc.TryGetValue("creditBilling", out var value) || !value.IsBsonDocument) return false;
        var billing = value.AsBsonDocument;
        var status = ReadString(billing, "status");
        return (status.Equals("pending", StringComparison.OrdinalIgnoreCase)
                || status.Equals("partial", StringComparison.OrdinalIgnoreCase))
               && ReadDecimal(billing, "balanceDue") > 0.009m;
    }

    private static bool IsOnlineCod(BsonDocument doc) =>
        ReadString(doc, "salesChannel").Equals("online", StringComparison.OrdinalIgnoreCase);

    private static decimal SumLineQty(BsonDocument doc, string field, bool returnLines)
    {
        var lines = ReadLines(doc, field);
        if (returnLines && lines.Count == 0) lines = ReadLines(doc, "lines");
        return lines.Sum(line =>
        {
            var qty = returnLines ? ReadDecimal(line, "returnQty") : 0;
            return qty > 0 ? qty : ReadDecimal(line, "qty");
        });
    }

    private static List<BsonDocument> ReadLines(BsonDocument doc, string field) =>
        doc.TryGetValue(field, out var value) && value.IsBsonArray
            ? value.AsBsonArray.OfType<BsonDocument>().ToList()
            : [];

    private static DateTimeOffset? ReadOccurredAt(BsonDocument doc)
    {
        foreach (var field in new[] { "createdAtUtc", "createdAt" })
        {
            if (!doc.TryGetValue(field, out var value) || value.IsBsonNull) continue;
            if (value.IsBsonDateTime)
                return new DateTimeOffset(value.ToUniversalTime(), TimeSpan.Zero);
            if (DateTimeOffset.TryParse(value.ToString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var parsed))
                return parsed;
        }
        return null;
    }

    private static bool IsInBusinessRange(DateTimeOffset occurred, DateTime from, DateTime to)
    {
        var date = occurred.ToOffset(BusinessOffset).Date;
        return date >= from.Date && date <= to.Date;
    }

    private static string FormatBusinessYmd(DateTimeOffset occurred) =>
        occurred.ToOffset(BusinessOffset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string CustomerKey(string code, string name, string phone)
    {
        if (!string.IsNullOrWhiteSpace(code)) return $"code:{code.ToUpperInvariant()}";
        var digits = NormalizePhone(phone);
        if (digits.Length > 0) return $"phone:{digits}";
        var normalizedName = string.Join(" ", name.ToLowerInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalizedName.Length > 0 ? $"name:{normalizedName}" : "walk-in";
    }

    private static bool IsPosted(BsonDocument doc) =>
        FirstNonEmpty(ReadString(doc, "status"), "posted")
            .Equals("posted", StringComparison.OrdinalIgnoreCase);

    private static string ReadString(BsonDocument doc, string field) =>
        doc.TryGetValue(field, out var value) && !value.IsBsonNull
            ? (value.IsString ? value.AsString : value.ToString() ?? "").Trim()
            : "";

    private static decimal ReadDecimal(BsonDocument doc, string field)
    {
        if (!doc.TryGetValue(field, out var value) || value.IsBsonNull) return 0;
        if (value.IsNumeric) return (decimal)value.ToDouble();
        return decimal.TryParse((value.ToString() ?? "").Replace(",", ""), NumberStyles.Any,
            CultureInfo.InvariantCulture, out var parsed) ? Round(parsed) : 0;
    }

    private static string ReadNestedString(BsonDocument doc, string parent, string field) =>
        doc.TryGetValue(parent, out var value) && value.IsBsonDocument
            ? ReadString(value.AsBsonDocument, field)
            : "";

    private static decimal ReadNestedDecimal(BsonDocument doc, string parent, string field) =>
        doc.TryGetValue(parent, out var value) && value.IsBsonDocument
            ? ReadDecimal(value.AsBsonDocument, field)
            : 0;

    private static string NormalizePhone(string value) => new(value.Where(char.IsDigit).ToArray());
    private static bool PhoneContains(string phone, string query)
    {
        var digits = NormalizePhone(query);
        return digits.Length > 0
            ? NormalizePhone(phone).Contains(digits, StringComparison.Ordinal)
            : phone.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
    private static decimal FirstPositive(params decimal[] values) =>
        values.FirstOrDefault(value => value > 0);
    private static decimal SumPayments(CustomerBillingPaymentSplits p) =>
        Round(p.Cash + p.Card + p.Upi + p.CreditNote);
    private static void RoundPayments(CustomerBillingPaymentSplits p)
    {
        p.Cash = Round(p.Cash);
        p.Card = Round(p.Card);
        p.Upi = Round(p.Upi);
        p.CreditNote = Round(p.CreditNote);
    }
    private static void AddPayments(CustomerBillingPaymentSplits target, CustomerBillingPaymentSplits source)
    {
        target.Cash = Round(target.Cash + source.Cash);
        target.Card = Round(target.Card + source.Card);
        target.Upi = Round(target.Upi + source.Upi);
        target.CreditNote = Round(target.CreditNote + source.CreditNote);
    }
    private static decimal Round(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);
    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string FormatYmd(DateTime value) =>
        value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static InvalidOperationException BuildCentralError(
        string operation,
        System.Net.HttpStatusCode statusCode,
        string responseBody)
    {
        var detail = string.IsNullOrWhiteSpace(responseBody) ? "No error details were returned." : responseBody.Trim();
        return new InvalidOperationException(
            $"Central customer billing report {operation} failed ({(int)statusCode}): {detail}");
    }

    private static void Validate(CustomerBillingReportQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.From.Date > query.To.Date)
            throw new ArgumentException("From date must be on or before to date.", nameof(query));
        if (query.Limit < 1 || query.Limit > MaxRows)
            throw new ArgumentOutOfRangeException(nameof(query), $"Limit must be between 1 and {MaxRows}.");
    }
}
