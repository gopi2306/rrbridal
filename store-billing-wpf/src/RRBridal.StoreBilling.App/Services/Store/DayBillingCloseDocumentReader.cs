using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Bson;
using RRBridal.StoreBilling.App.Services.Payments;

namespace RRBridal.StoreBilling.App.Services.Store;

/// <summary>Pure helpers for day-close bill/outbox parsing (unit-testable).</summary>
public static class DayBillingCloseDocumentReader
{
    public static bool IsPostedBill(BsonDocument doc)
    {
        var status = ReadString(doc, "status") ?? "posted";
        return string.Equals(status, "posted", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPostedReturn(BsonDocument doc)
    {
        var status = ReadString(doc, "status") ?? "posted";
        return string.Equals(status, "posted", StringComparison.OrdinalIgnoreCase);
    }

    public static ReturnDayTotals AggregateReturnDayTotals(IEnumerable<BsonDocument> returns)
    {
        int count = 0;
        decimal returnTotalAmount = 0m;
        decimal cashRefundTotal = 0m;
        decimal creditNoteIssuedTotal = 0m;
        decimal exchangeCash = 0m, exchangeCard = 0m, exchangeUpi = 0m, exchangeCn = 0m;

        foreach (var doc in returns)
        {
            if (!IsPostedReturn(doc))
                continue;

            count++;
            returnTotalAmount += ReadDecimal(doc, "returnTotal");

            var returnMode = ReadString(doc, "returnMode") ?? "";
            var cashRefunded = ReadDecimal(doc, "cashRefunded");
            var creditBalance = ReadDecimal(doc, "creditBalance");
            if (string.Equals(returnMode, "cash_refund", StringComparison.OrdinalIgnoreCase))
            {
                var refundAmt = cashRefunded > 0 ? cashRefunded : creditBalance;
                if (refundAmt > 0)
                    cashRefundTotal += refundAmt;
            }
            else if (string.Equals(returnMode, "credit_note", StringComparison.OrdinalIgnoreCase) && creditBalance > 0)
            {
                creditNoteIssuedTotal += creditBalance;
            }

            var amountCollected = ReadDecimal(doc, "amountCollected");
            if (amountCollected > 0)
            {
                var payments = SumBillPayments(doc);
                exchangeCash += payments.Cash;
                exchangeCard += payments.Card;
                exchangeUpi += payments.Upi;
                exchangeCn += payments.CreditNote;
            }
        }

        return new ReturnDayTotals(
            count,
            returnTotalAmount,
            cashRefundTotal,
            creditNoteIssuedTotal,
            new PaymentDayTotals(exchangeCash, exchangeCard, exchangeUpi, exchangeCn));
    }

    public static decimal AggregateCreditNoteCashoutDayTotals(IEnumerable<BsonDocument> cashouts)
    {
        decimal total = 0m;
        foreach (var doc in cashouts)
        {
            if (!IsPostedCashout(doc))
                continue;
            total += ReadDecimal(doc, "cashRefunded");
        }

        return total;
    }

    public static bool IsPostedCashout(BsonDocument doc)
    {
        var status = ReadString(doc, "status") ?? "posted";
        return string.Equals(status, "posted", StringComparison.OrdinalIgnoreCase);
    }

    public static string FormatReturnPaymentSummary(BsonDocument doc)
    {
        var amountCollected = ReadDecimal(doc, "amountCollected");
        if (amountCollected <= 0)
            return "—";

        var payments = SumBillPayments(doc);
        var parts = new List<string>();
        if (payments.Cash > 0)
            parts.Add($"Cash {payments.Cash:N2}");
        if (payments.Card > 0)
            parts.Add($"Card {payments.Card:N2}");
        if (payments.Upi > 0)
            parts.Add($"UPI {payments.Upi:N2}");
        if (payments.CreditNote > 0)
            parts.Add($"CN {payments.CreditNote:N2}");
        return parts.Count > 0 ? string.Join(", ", parts) : "—";
    }

    public static bool MatchesLocalDay(BsonDocument doc, DateTime localDate)
    {
        if (!TryGetUtcDate(doc, "createdAtUtc", out var createdUtc))
            return false;
        return createdUtc.ToLocalTime().Date == localDate.Date;
    }

    public static bool MatchesPosCounterFilter(BsonDocument doc, string? posCounterFilter)
    {
        if (string.IsNullOrWhiteSpace(posCounterFilter))
            return true;
        var pos = ReadString(doc, "posCounter") ?? "";
        return string.Equals(pos.Trim(), posCounterFilter.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static decimal SumBillLineQty(BsonDocument doc)
    {
        if (!doc.TryGetValue("lines", out var linesVal) || !linesVal.IsBsonArray)
            return 0m;

        decimal total = 0m;
        foreach (BsonDocument line in linesVal.AsBsonArray.OfType<BsonDocument>())
        {
            var amount = ReadDecimal(line, "amount");
            var taxable = ReadDecimal(line, "revisedAmount");
            if (amount <= 0 && taxable <= 0)
                continue;
            total += ReadDecimal(line, "qty");
        }

        return total;
    }

    public static PaymentDayTotals SumBillPayments(BsonDocument doc)
    {
        if (!doc.TryGetValue("payments", out var payVal) || !payVal.IsBsonArray)
            return new PaymentDayTotals(0m, 0m, 0m, 0m);

        decimal cash = 0m, card = 0m, upi = 0m, creditNote = 0m;
        foreach (BsonDocument p in payVal.AsBsonArray.OfType<BsonDocument>())
        {
            var providerName = ReadString(p, "provider") ?? "";
            if (!Enum.TryParse<PaymentProviderKind>(providerName, true, out var kind))
                continue;
            var amount = ReadDecimal(p, "amount");
            switch (kind)
            {
                case PaymentProviderKind.Cash:
                    cash += amount;
                    break;
                case PaymentProviderKind.PineLabs:
                    card += amount;
                    break;
                case PaymentProviderKind.Razorpay:
                    upi += amount;
                    break;
                case PaymentProviderKind.CreditNote:
                    creditNote += amount;
                    break;
            }
        }

        return new PaymentDayTotals(cash, card, upi, creditNote);
    }

    public static Dictionary<string, string> BuildOutboxSyncByBillNo(IEnumerable<BsonDocument> outboxEvents)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var evt in outboxEvents)
        {
            if (!string.Equals(ReadString(evt, "type"), "InvoiceCreated", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!evt.TryGetValue("payload", out var payloadVal) || !payloadVal.IsBsonDocument)
                continue;

            var payload = payloadVal.AsBsonDocument;
            var billNo = ReadString(payload, "billNo") ?? ReadString(payload, "invoiceNo") ?? "";
            if (string.IsNullOrWhiteSpace(billNo))
                continue;

            var status = ReadString(evt, "status") ?? "pending";
            if (map.TryGetValue(billNo, out var existing))
            {
                if (string.Equals(existing, "synced", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.Equals(status, "synced", StringComparison.OrdinalIgnoreCase))
                    map[billNo] = status;
            }
            else
            {
                map[billNo] = status;
            }
        }

        return map;
    }

    public static string ResolveSyncStatus(BsonDocument billDoc, IReadOnlyDictionary<string, string> outboxByBillNo)
    {
        var billNo = ReadString(billDoc, "billNo") ?? "";
        if (!string.IsNullOrWhiteSpace(billNo)
            && outboxByBillNo.TryGetValue(billNo, out var status))
        {
            if (string.Equals(status, "synced", StringComparison.OrdinalIgnoreCase))
                return "Synced";
            if (string.Equals(status, "pending", StringComparison.OrdinalIgnoreCase))
                return "Pending sync";
        }

        if (HasOutboxFailureWarning(billDoc))
            return "Not queued";

        return "Unknown";
    }

    public static bool HasOutboxFailureWarning(BsonDocument doc)
    {
        if (!doc.TryGetValue("postWarnings", out var w) || !w.IsBsonArray)
            return false;
        foreach (var item in w.AsBsonArray)
        {
            if (!item.IsString)
                continue;
            if (item.AsString.Contains("Outbox", StringComparison.OrdinalIgnoreCase)
                || item.AsString.Contains("enqueue", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static bool TryGetUtcDate(BsonDocument doc, string key, out DateTime utc)
    {
        utc = default;
        if (!doc.TryGetValue(key, out var v) || v.IsBsonNull || !v.IsString)
            return false;
        if (!DateTime.TryParse(v.AsString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            return false;
        utc = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
        return true;
    }

    public static decimal ReadDecimal(BsonDocument doc, string key)
    {
        if (!doc.TryGetValue(key, out var v) || v.IsBsonNull)
            return 0m;
        return v switch
        {
            { IsDouble: true } => (decimal)v.AsDouble,
            { IsInt32: true } => v.AsInt32,
            { IsInt64: true } => v.AsInt64,
            { IsDecimal128: true } => (decimal)v.AsDecimal128,
            { IsBoolean: true } => v.AsBoolean ? 1m : 0m,
            { IsString: true } => decimal.TryParse(v.AsString, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m,
            _ => 0m,
        };
    }

    public static string? ReadString(BsonDocument doc, string key)
    {
        if (!doc.TryGetValue(key, out var v) || v.IsBsonNull)
            return null;
        return v.IsString ? v.AsString : v.ToString();
    }

    public static decimal SumDailyExpensesForBusinessDate(
        IEnumerable<BsonDocument> expenses,
        string businessDate,
        string? posCounterFilter)
    {
        return expenses
            .Where(d => string.Equals(ReadString(d, "businessDate"), businessDate, StringComparison.Ordinal))
            .Where(d => MatchesPosCounterFilter(d, posCounterFilter))
            .Where(d => string.Equals(ReadString(d, "status") ?? "posted", "posted", StringComparison.OrdinalIgnoreCase))
            .Sum(d => ReadDecimal(d, "amount"));
    }

    public static (decimal Deposits, decimal Withdrawals) SumCashMovementsForBusinessDate(
        IEnumerable<BsonDocument> movements,
        string businessDate,
        string? posCounterFilter)
    {
        decimal deposits = 0m, withdrawals = 0m;
        foreach (var doc in movements)
        {
            if (!string.Equals(ReadString(doc, "businessDate"), businessDate, StringComparison.Ordinal))
                continue;
            if (!MatchesPosCounterFilter(doc, posCounterFilter))
                continue;
            if (!string.Equals(ReadString(doc, "status") ?? "posted", "posted", StringComparison.OrdinalIgnoreCase))
                continue;

            var amount = ReadDecimal(doc, "amount");
            var type = ReadString(doc, "movementType") ?? "";
            if (string.Equals(type, CashMovementType.DepositToBank, StringComparison.OrdinalIgnoreCase))
                deposits += amount;
            else if (string.Equals(type, CashMovementType.CashWithdrawal, StringComparison.OrdinalIgnoreCase))
                withdrawals += amount;
        }

        return (deposits, withdrawals);
    }

    public static string FormatBillCreditNoteReferences(BsonDocument doc)
    {
        if (!doc.TryGetValue("payments", out var payVal) || !payVal.IsBsonArray)
            return "";

        var refs = new List<string>();
        foreach (BsonDocument p in payVal.AsBsonArray.OfType<BsonDocument>())
        {
            var provider = ReadString(p, "provider") ?? "";
            if (!string.Equals(provider, "CreditNote", StringComparison.OrdinalIgnoreCase))
                continue;
            var reference = ReadString(p, "reference")?.Trim();
            if (!string.IsNullOrWhiteSpace(reference))
                refs.Add(reference);
        }

        return string.Join("; ", refs);
    }

    public static string FormatUtcLocal(string? utcIso)
    {
        if (string.IsNullOrWhiteSpace(utcIso))
            return "—";
        if (!DateTime.TryParse(utcIso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            return "—";
        var utc = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
        return utc.ToLocalTime().ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture);
    }

    public static IEnumerable<BsonDocument> FilterAdjustmentsForLocalDay(
        IEnumerable<BsonDocument> adjustments,
        DateTime localDate,
        string? posCounterFilter)
        => adjustments
            .Where(d => string.Equals(ReadString(d, "status") ?? "posted", "posted", StringComparison.OrdinalIgnoreCase))
            .Where(d => MatchesLocalDay(d, localDate))
            .Where(d => MatchesPosCounterFilter(d, posCounterFilter));

    public static IEnumerable<BsonDocument> FilterExpensesForBusinessDate(
        IEnumerable<BsonDocument> expenses,
        string businessDate,
        string? posCounterFilter)
        => expenses
            .Where(d => string.Equals(ReadString(d, "businessDate"), businessDate, StringComparison.Ordinal))
            .Where(d => MatchesPosCounterFilter(d, posCounterFilter))
            .Where(d => string.Equals(ReadString(d, "status") ?? "posted", "posted", StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<BsonDocument> FilterCashMovementsForBusinessDate(
        IEnumerable<BsonDocument> movements,
        string businessDate,
        string? posCounterFilter)
    {
        foreach (var doc in movements)
        {
            if (!string.Equals(ReadString(doc, "businessDate"), businessDate, StringComparison.Ordinal))
                continue;
            if (!MatchesPosCounterFilter(doc, posCounterFilter))
                continue;
            if (!string.Equals(ReadString(doc, "status") ?? "posted", "posted", StringComparison.OrdinalIgnoreCase))
                continue;
            yield return doc;
        }
    }

    public static IEnumerable<BsonDocument> FilterCreditNoteCashoutsForLocalDay(
        IEnumerable<BsonDocument> cashouts,
        DateTime localDate,
        string? posCounterFilter)
        => cashouts
            .Where(IsPostedCashout)
            .Where(d => MatchesLocalDay(d, localDate))
            .Where(d => MatchesPosCounterFilter(d, posCounterFilter));
}
