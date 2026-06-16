using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services.Payments;
using RRBridal.StoreBilling.App.Services.Sync;

namespace RRBridal.StoreBilling.App.Services.Billing;

public sealed class OnlineCodSearchRow
{
    public required string BillNo { get; init; }
    public string BillDate { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public string CustomerPhone { get; init; } = "";
    public decimal Amount { get; init; }
    public string Status { get; init; } = "";
    public string TransactionNo { get; init; } = "";
    public string ReceivedPaymentMode { get; init; } = "";
    public string CreatedAtDisplay { get; init; } = "";
    public DateTime SortUtc { get; init; }
}

public sealed class OnlineCodPendingBalance
{
    public decimal BalanceTill { get; init; }
    public int PendingCount { get; init; }
    public int ReceivedTodayCount { get; init; }
    public decimal ReceivedTodayAmount { get; init; }
}

public enum CodReceivedPaymentMode
{
    Cash,
    UPI,
    Card,
}

public sealed class OnlineCodBillService
{
    private readonly IMongoCollection<BsonDocument> _bills;
    private readonly BillingOutboxPublisher _outbox;

    public OnlineCodBillService(IMongoDatabase localDb, BillingOutboxPublisher outbox)
    {
        _bills = localDb.GetCollection<BsonDocument>("store_bills");
        _outbox = outbox;
    }

    public async Task<OnlineCodPendingBalance> GetPendingBalanceAsync(string storeId, CancellationToken ct = default)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", storeId?.Trim() ?? ""),
            Builders<BsonDocument>.Filter.Eq("status", "posted"),
            Builders<BsonDocument>.Filter.Eq("salesChannel", OnlineCodDocumentReader.SalesChannelOnline));

        var docs = await _bills.Find(filter).ToListAsync(ct);
        var todayLocal = DateTime.Today;

        decimal balanceTill = 0m;
        int pendingCount = 0;
        int receivedToday = 0;
        decimal receivedTodayAmount = 0m;

        foreach (var doc in docs)
        {
            if (OnlineCodDocumentReader.IsOnlineCodPending(doc))
            {
                pendingCount++;
                balanceTill += OnlineCodDocumentReader.ReadOnlineCodAmount(doc);
                continue;
            }

            if (!doc.TryGetValue("onlineCod", out var oc) || !oc.IsBsonDocument)
                continue;
            var cod = oc.AsBsonDocument;
            var receivedAt = ReadString(cod, "receivedAtUtc");
            if (string.IsNullOrWhiteSpace(receivedAt))
                continue;
            if (!DateTime.TryParse(receivedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                continue;
            var local = dt.ToLocalTime().Date;
            if (local == todayLocal)
            {
                receivedToday++;
                receivedTodayAmount += OnlineCodDocumentReader.ReadOnlineCodAmount(doc);
            }
        }

        return new OnlineCodPendingBalance
        {
            BalanceTill = balanceTill,
            PendingCount = pendingCount,
            ReceivedTodayCount = receivedToday,
            ReceivedTodayAmount = receivedTodayAmount,
        };
    }

    public async Task<decimal> GetPendingTotalForBusinessDateAsync(
        string storeId,
        DateTime localDate,
        CancellationToken ct = default)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", storeId?.Trim() ?? ""),
            Builders<BsonDocument>.Filter.Eq("status", "posted"),
            Builders<BsonDocument>.Filter.Eq("salesChannel", OnlineCodDocumentReader.SalesChannelOnline));

        var docs = await _bills.Find(filter).ToListAsync(ct);
        return docs
            .Where(d => OnlineCodDocumentReader.IsOnlineCodPending(d))
            .Where(d => OnlineCodDocumentReader.ReadSortUtc(d).ToLocalTime().Date == localDate.Date)
            .Sum(OnlineCodDocumentReader.ReadOnlineCodAmount);
    }

    public async Task<IReadOnlyList<OnlineCodSearchRow>> SearchAsync(
        string storeId,
        string? billNo,
        string? customerName,
        string? statusFilter,
        DateTime? dateFrom,
        DateTime? dateTo,
        int limit = 200,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 500);
        var filters = new List<FilterDefinition<BsonDocument>>
        {
            Builders<BsonDocument>.Filter.Eq("storeId", storeId?.Trim() ?? ""),
            Builders<BsonDocument>.Filter.Eq("status", "posted"),
            Builders<BsonDocument>.Filter.Eq("salesChannel", OnlineCodDocumentReader.SalesChannelOnline),
        };

        if (!string.IsNullOrWhiteSpace(billNo))
        {
            var safe = Regex.Escape(billNo.Trim());
            filters.Add(Builders<BsonDocument>.Filter.Regex("billNo", new BsonRegularExpression(safe, "i")));
        }

        if (!string.IsNullOrWhiteSpace(customerName))
        {
            var safe = Regex.Escape(customerName.Trim());
            filters.Add(Builders<BsonDocument>.Filter.Regex("customerName", new BsonRegularExpression(safe, "i")));
        }

        var docs = await _bills
            .Find(Builders<BsonDocument>.Filter.And(filters))
            .Sort(Builders<BsonDocument>.Sort.Descending("createdAtUtc"))
            .Limit(limit * 3)
            .ToListAsync(ct);

        return docs
            .Select(MapSearchRow)
            .Where(r => r != null)
            .Cast<OnlineCodSearchRow>()
            .Where(r => MatchesStatusFilter(r, statusFilter))
            .Where(r => OnlineCodDocumentReader.InCreatedDateRange(r.SortUtc, dateFrom, dateTo))
            .Take(limit)
            .ToList();
    }

    public async Task<bool> RecordPaymentReceivedAsync(
        string storeId,
        string billNo,
        CodReceivedPaymentMode paymentMode,
        string transactionNo,
        string receivedBy,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(billNo) || string.IsNullOrWhiteSpace(transactionNo))
            return false;

        var doc = await _bills.Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", storeId?.Trim() ?? ""),
            Builders<BsonDocument>.Filter.Eq("billNo", billNo.Trim()),
            Builders<BsonDocument>.Filter.Eq("status", "posted"))).FirstOrDefaultAsync(ct);

        if (doc == null || !OnlineCodDocumentReader.IsOnlineCodPending(doc))
            return false;

        var amount = OnlineCodDocumentReader.ReadOnlineCodAmount(doc);
        var (provider, modeLabel) = MapPaymentMode(paymentMode);
        var receivedAt = DateTime.UtcNow.ToString("O");

        var paymentsArr = new BsonArray
        {
            new BsonDocument
            {
                { "provider", provider.ToString() },
                { "amount", (double)amount },
                { "reference", transactionNo.Trim() },
                { "status", "posted" },
            },
        };

        var onlineCod = new BsonDocument
        {
            { "status", OnlineCodDocumentReader.StatusReceived },
            { "amount", (double)amount },
            { "transactionNo", transactionNo.Trim() },
            { "receivedAtUtc", receivedAt },
            { "receivedBy", receivedBy?.Trim() ?? "" },
            { "receivedPaymentMode", modeLabel },
        };

        var update = Builders<BsonDocument>.Update
            .Set("onlineCod", onlineCod)
            .Set("payments", paymentsArr)
            .Set("paymentMode", modeLabel)
            .Push("codPaymentHistory", new BsonDocument
            {
                { "receivedAtUtc", receivedAt },
                { "receivedBy", receivedBy?.Trim() ?? "" },
                { "transactionNo", transactionNo.Trim() },
                { "receivedPaymentMode", modeLabel },
                { "amount", (double)amount },
            });

        var result = await _bills.UpdateOneAsync(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("storeId", storeId?.Trim() ?? ""),
                Builders<BsonDocument>.Filter.Eq("billNo", billNo.Trim()),
                Builders<BsonDocument>.Filter.Eq("salesChannel", OnlineCodDocumentReader.SalesChannelOnline),
                Builders<BsonDocument>.Filter.Eq("onlineCod.status", OnlineCodDocumentReader.StatusPending)),
            update,
            cancellationToken: ct);

        if (result.ModifiedCount == 0)
            return false;

        var updated = await _bills.Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", storeId?.Trim() ?? ""),
            Builders<BsonDocument>.Filter.Eq("billNo", billNo.Trim()))).FirstOrDefaultAsync(ct);

        if (updated != null)
            await _outbox.PublishInvoiceCodPaymentReceivedAsync(updated, ct);

        return true;
    }

    private static (PaymentProviderKind Provider, string ModeLabel) MapPaymentMode(CodReceivedPaymentMode mode) =>
        mode switch
        {
            CodReceivedPaymentMode.Card => (PaymentProviderKind.PineLabs, "Card"),
            CodReceivedPaymentMode.UPI => (PaymentProviderKind.Razorpay, "UPI"),
            _ => (PaymentProviderKind.Cash, "Cash"),
        };

    private static bool MatchesStatusFilter(OnlineCodSearchRow row, string? statusFilter)
    {
        if (string.IsNullOrWhiteSpace(statusFilter) || string.Equals(statusFilter, "all", StringComparison.OrdinalIgnoreCase))
            return true;
        return string.Equals(row.Status, statusFilter, StringComparison.OrdinalIgnoreCase);
    }

    private static OnlineCodSearchRow? MapSearchRow(BsonDocument doc)
    {
        var billNo = ReadString(doc, "billNo") ?? "";
        if (string.IsNullOrEmpty(billNo))
            return null;

        var sortUtc = OnlineCodDocumentReader.ReadSortUtc(doc);
        var createdDisplay = sortUtc == DateTime.MinValue
            ? "—"
            : sortUtc.ToLocalTime().ToString("dd-MMM-yyyy", CultureInfo.GetCultureInfo("en-IN"));

        return new OnlineCodSearchRow
        {
            BillNo = billNo,
            BillDate = ReadString(doc, "billDate") ?? createdDisplay,
            CustomerName = ReadString(doc, "customerName") ?? "",
            CustomerPhone = ReadString(doc, "customerPhone") ?? "",
            Amount = OnlineCodDocumentReader.ReadOnlineCodAmount(doc),
            Status = OnlineCodDocumentReader.ReadOnlineCodStatus(doc),
            TransactionNo = OnlineCodDocumentReader.ReadTransactionNo(doc) ?? "",
            ReceivedPaymentMode = OnlineCodDocumentReader.ReadReceivedPaymentMode(doc) ?? "",
            CreatedAtDisplay = createdDisplay,
            SortUtc = sortUtc,
        };
    }

    private static string? ReadString(BsonDocument d, string key) =>
        d.TryGetValue(key, out var v) && !v.IsBsonNull
            ? v.IsString ? v.AsString : v.ToString()
            : null;
}
