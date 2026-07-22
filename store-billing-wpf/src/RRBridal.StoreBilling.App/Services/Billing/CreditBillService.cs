using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services.Invoicing;
using RRBridal.StoreBilling.App.Services.Payments;
using RRBridal.StoreBilling.App.Services.Sync;

namespace RRBridal.StoreBilling.App.Services.Billing;

public sealed class CreditBillSearchRow
{
    public required string BillNo { get; init; }
    public string BillDate { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public string CustomerPhone { get; init; } = "";
    public decimal TotalPayable { get; init; }
    public decimal AdvancePaid { get; init; }
    public decimal AmountPaid { get; init; }
    public decimal BalanceDue { get; init; }
    public string Status { get; init; } = "";
    public DateTime SortUtc { get; init; }
}

public sealed class CreditBillPendingBalance
{
    public decimal BalanceTill { get; init; }
    public int PendingCount { get; init; }
}

public enum CreditReceivedPaymentMode
{
    Cash,
    UPI,
    Card,
    CreditNote,
    Split,
}

public sealed class CreditPaymentLeg
{
    public CreditReceivedPaymentMode Mode { get; init; }
    public decimal Amount { get; init; }
    public string Reference { get; init; } = "";
}

public sealed class CreditPaymentResult
{
    public bool Success { get; init; }
    public string? ReceiptNo { get; init; }
    public decimal AmountPaid { get; init; }
    public decimal BalanceDue { get; init; }
    public string? Error { get; init; }
}

public sealed class CreditBillService
{
    private readonly IMongoCollection<BsonDocument> _bills;
    private readonly IMongoCollection<BsonDocument> _receipts;
    private readonly BillingOutboxPublisher _outbox;
    private readonly BillNumberGenerator _billNumbers;

    public CreditBillService(
        IMongoDatabase localDb,
        BillingOutboxPublisher outbox,
        BillNumberGenerator billNumbers)
    {
        _bills = localDb.GetCollection<BsonDocument>("store_bills");
        _receipts = localDb.GetCollection<BsonDocument>("store_payment_receipts");
        _outbox = outbox;
        _billNumbers = billNumbers;
    }

    public async Task<CreditBillPendingBalance> GetPendingBalanceAsync(string storeId, CancellationToken ct = default)
    {
        var docs = await FindCreditBillsAsync(storeId, ct);
        decimal balance = 0m;
        var count = 0;
        foreach (var doc in docs)
        {
            if (!CreditBillDocumentReader.IsOpen(doc))
                continue;
            count++;
            balance += CreditBillDocumentReader.ReadBalanceDue(doc);
        }

        return new CreditBillPendingBalance { BalanceTill = balance, PendingCount = count };
    }

    public async Task<IReadOnlyList<CreditBillSearchRow>> SearchAsync(
        string storeId,
        string? billNo,
        string? customerName,
        string? customerPhone,
        string? statusFilter,
        int limit = 200,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 500);
        var docs = await FindCreditBillsAsync(storeId, ct);
        IEnumerable<BsonDocument> query = docs;

        if (!string.IsNullOrWhiteSpace(billNo))
        {
            var q = billNo.Trim();
            query = query.Where(d =>
                (d.GetValue("billNo", "").AsString ?? "").Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(customerName))
        {
            var n = customerName.Trim();
            query = query.Where(d =>
                (d.GetValue("customerName", "").AsString ?? "").Contains(n, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(customerPhone))
        {
            var digits = Regex.Replace(customerPhone, @"\D", "");
            query = query.Where(d =>
            {
                var phone = Regex.Replace(d.GetValue("customerPhone", "").AsString ?? "", @"\D", "");
                return phone.Contains(digits, StringComparison.Ordinal);
            });
        }

        if (!string.IsNullOrWhiteSpace(statusFilter)
            && !string.Equals(statusFilter, "all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(d =>
                string.Equals(CreditBillDocumentReader.ReadStatus(d), statusFilter, StringComparison.OrdinalIgnoreCase));
        }

        return query
            .Select(MapRow)
            .Where(r => r != null)
            .Cast<CreditBillSearchRow>()
            .OrderByDescending(r => r.SortUtc)
            .Take(limit)
            .ToList();
    }

    public async Task<IReadOnlyList<CreditBillSearchRow>> GetPendingListAsync(
        string storeId,
        int limit = 20,
        CancellationToken ct = default)
    {
        var rows = await SearchAsync(storeId, null, null, null, "all", limit: 500, ct);
        return rows
            .Where(r => string.Equals(r.Status, CreditBillDocumentReader.StatusPending, StringComparison.OrdinalIgnoreCase)
                || string.Equals(r.Status, CreditBillDocumentReader.StatusPartial, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.SortUtc)
            .Take(limit)
            .ToList();
    }

    public async Task<CreditPaymentResult> RecordPaymentAsync(
        string storeId,
        string billNo,
        decimal amount,
        CreditReceivedPaymentMode paymentMode,
        string transactionNo,
        string receivedBy,
        bool allowPartial,
        IReadOnlyList<CreditPaymentLeg>? splitLegs = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(billNo) || amount <= 0)
            return new CreditPaymentResult { Success = false, Error = "Invalid amount or bill." };

        var doc = await _bills.Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", storeId?.Trim() ?? ""),
            Builders<BsonDocument>.Filter.Eq("billNo", billNo.Trim()),
            Builders<BsonDocument>.Filter.Eq("status", "posted"))).FirstOrDefaultAsync(ct);

        if (doc == null || !CreditBillDocumentReader.IsOpen(doc))
            return new CreditPaymentResult { Success = false, Error = "Open credit bill not found." };

        var balance = CreditBillDocumentReader.ReadBalanceDue(doc);
        var payAmount = MoneyMath.RoundDisplayAmount(amount);
        if (payAmount > balance + 0.009m)
            return new CreditPaymentResult { Success = false, Error = "Amount exceeds balance due." };

        if (!allowPartial && payAmount + 0.009m < balance)
            return new CreditPaymentResult { Success = false, Error = "Partial collection is disabled. Enter the full balance." };

        var receiptNo = await _billNumbers.NextPaymentReceiptAsync(ct);
        var legs = BuildPaymentLegs(paymentMode, payAmount, transactionNo, splitLegs);
        if (legs.Count == 0)
            return new CreditPaymentResult { Success = false, Error = "Invalid payment details." };

        var modeLabel = paymentMode == CreditReceivedPaymentMode.Split
            ? "Split"
            : legs[0].ModeLabel;
        var reference = paymentMode == CreditReceivedPaymentMode.Split
            ? string.Join(", ", legs.Select(l => string.IsNullOrWhiteSpace(l.Reference) ? l.ModeLabel : $"{l.ModeLabel}:{l.Reference}"))
            : transactionNo?.Trim() ?? "";
        var receivedAt = DateTime.UtcNow.ToString("O");
        var totalPayable = CreditBillDocumentReader.ReadTotalPayable(doc);
        var previousPaid = CreditBillDocumentReader.ReadAmountPaid(doc);
        var newPaid = MoneyMath.RoundDisplayAmount(previousPaid + payAmount);
        var newBalance = Math.Max(0m, MoneyMath.RoundDisplayAmount(totalPayable - newPaid));
        var newStatus = CreditBillDocumentReader.ResolveStatus(totalPayable, newPaid);
        var advanceAtPost = CreditBillDocumentReader.ReadAdvanceAtPost(doc);

        var paymentEntry = new BsonDocument
        {
            { "kind", "partial" },
            { "receivedAtUtc", receivedAt },
            { "amount", (double)payAmount },
            { "mode", modeLabel },
            { "reference", reference },
            { "receivedBy", receivedBy?.Trim() ?? "" },
            { "receiptNo", receiptNo },
        };
        if (paymentMode == CreditReceivedPaymentMode.Split)
        {
            paymentEntry["legs"] = new BsonArray(legs.Select(l => new BsonDocument
            {
                { "mode", l.ModeLabel },
                { "amount", (double)l.Amount },
                { "reference", l.Reference },
            }));
        }

        var creditBilling = CreditBillDocumentReader.BuildCreditBillingDocument(
            totalPayable,
            advanceAtPost,
            newPaid,
            doc.GetValue("creditBilling", new BsonDocument()).AsBsonDocument.GetValue("creditCustomer", false).AsBoolean);

        creditBilling["status"] = newStatus;
        creditBilling["balanceDue"] = (double)newBalance;

        var paymentsArr = new BsonArray();
        var existingCb = CreditBillDocumentReader.GetCreditBilling(doc);
        if (existingCb != null && existingCb.TryGetValue("payments", out var existingPayments) && existingPayments.IsBsonArray)
        {
            foreach (var p in existingPayments.AsBsonArray)
                paymentsArr.Add(p.DeepClone());
        }

        paymentsArr.Add(paymentEntry);
        creditBilling["payments"] = paymentsArr;

        var topPayments = doc.GetValue("payments", new BsonArray()).AsBsonArray;
        var newTop = new BsonArray();
        foreach (var p in topPayments)
            newTop.Add(p.DeepClone());
        foreach (var leg in legs)
        {
            newTop.Add(new BsonDocument
            {
                { "provider", leg.Provider.ToString() },
                { "amount", (double)leg.Amount },
                { "reference", leg.Reference },
                { "status", "posted" },
            });
        }

        var paymentModeLabel = newBalance <= 0.009m
            ? modeLabel
            : "Credit";

        var update = Builders<BsonDocument>.Update
            .Set("creditBilling", creditBilling)
            .Set("payments", newTop)
            .Set("paymentMode", paymentModeLabel);

        var result = await _bills.UpdateOneAsync(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("storeId", storeId?.Trim() ?? ""),
                Builders<BsonDocument>.Filter.Eq("billNo", billNo.Trim())),
            update,
            cancellationToken: ct);

        if (result.ModifiedCount == 0)
            return new CreditPaymentResult { Success = false, Error = "Could not update bill." };

        var receiptDoc = new BsonDocument
        {
            { "receiptNo", receiptNo },
            { "billNo", billNo.Trim() },
            { "storeId", storeId?.Trim() ?? "" },
            { "customerName", doc.GetValue("customerName", "").AsString },
            { "customerPhone", doc.GetValue("customerPhone", "").AsString },
            { "amount", (double)payAmount },
            { "mode", modeLabel },
            { "reference", reference },
            { "balanceDue", (double)newBalance },
            { "totalPayable", (double)totalPayable },
            { "amountPaid", (double)newPaid },
            { "receivedBy", receivedBy?.Trim() ?? "" },
            { "createdAtUtc", receivedAt },
        };
        await _receipts.InsertOneAsync(receiptDoc, cancellationToken: ct);

        var updated = await _bills.Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", storeId?.Trim() ?? ""),
            Builders<BsonDocument>.Filter.Eq("billNo", billNo.Trim()))).FirstOrDefaultAsync(ct);

        if (updated != null)
            await _outbox.PublishInvoiceCreditPaymentReceivedAsync(updated, receiptDoc, ct);

        return new CreditPaymentResult
        {
            Success = true,
            ReceiptNo = receiptNo,
            AmountPaid = payAmount,
            BalanceDue = newBalance,
        };
    }

    public async Task<BsonDocument?> GetPostedBillAsync(
        string storeId,
        string billNo,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(billNo))
            return null;

        return await _bills.Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", storeId?.Trim() ?? ""),
            Builders<BsonDocument>.Filter.Eq("billNo", billNo.Trim()),
            Builders<BsonDocument>.Filter.Eq("status", "posted"))).FirstOrDefaultAsync(ct);
    }

    public async Task<BsonDocument?> GetPaymentReceiptAsync(
        string storeId,
        string receiptNo,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(receiptNo))
            return null;

        return await _receipts.Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", storeId?.Trim() ?? ""),
            Builders<BsonDocument>.Filter.Eq("receiptNo", receiptNo.Trim()))).FirstOrDefaultAsync(ct);
    }

    private async Task<List<BsonDocument>> FindCreditBillsAsync(string storeId, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", storeId?.Trim() ?? ""),
            Builders<BsonDocument>.Filter.Eq("status", "posted"),
            Builders<BsonDocument>.Filter.Exists("creditBilling"));

        return await _bills.Find(filter).ToListAsync(ct);
    }

    private static CreditBillSearchRow? MapRow(BsonDocument doc)
    {
        var billNo = doc.GetValue("billNo", "").AsString;
        if (string.IsNullOrWhiteSpace(billNo) || !CreditBillDocumentReader.HasCreditBilling(doc))
            return null;

        return new CreditBillSearchRow
        {
            BillNo = billNo,
            BillDate = doc.GetValue("billDate", "").AsString,
            CustomerName = doc.GetValue("customerName", "").AsString,
            CustomerPhone = doc.GetValue("customerPhone", "").AsString,
            TotalPayable = CreditBillDocumentReader.ReadTotalPayable(doc),
            AdvancePaid = CreditBillDocumentReader.ReadAdvanceAtPost(doc),
            AmountPaid = CreditBillDocumentReader.ReadAmountPaid(doc),
            BalanceDue = CreditBillDocumentReader.ReadBalanceDue(doc),
            Status = CreditBillDocumentReader.ReadStatus(doc),
            SortUtc = CreditBillDocumentReader.ReadSortUtc(doc),
        };
    }

    private sealed record ResolvedCreditPaymentLeg(
        PaymentProviderKind Provider,
        string ModeLabel,
        decimal Amount,
        string Reference);

    private static List<ResolvedCreditPaymentLeg> BuildPaymentLegs(
        CreditReceivedPaymentMode paymentMode,
        decimal payAmount,
        string transactionNo,
        IReadOnlyList<CreditPaymentLeg>? splitLegs)
    {
        if (paymentMode == CreditReceivedPaymentMode.Split)
        {
            if (splitLegs == null || splitLegs.Count == 0)
                return new List<ResolvedCreditPaymentLeg>();

            var total = MoneyMath.RoundDisplayAmount(splitLegs.Sum(l => l.Amount));
            if (total != payAmount)
                return new List<ResolvedCreditPaymentLeg>();

            return splitLegs
                .Where(l => l.Amount > 0)
                .Select(l =>
                {
                    var (provider, modeLabel) = MapPaymentMode(l.Mode);
                    return new ResolvedCreditPaymentLeg(provider, modeLabel, l.Amount, l.Reference?.Trim() ?? "");
                })
                .ToList();
        }

        var (singleProvider, singleMode) = MapPaymentMode(paymentMode);
        return new List<ResolvedCreditPaymentLeg>
        {
            new(singleProvider, singleMode, payAmount, transactionNo?.Trim() ?? ""),
        };
    }

    private static (PaymentProviderKind Provider, string ModeLabel) MapPaymentMode(CreditReceivedPaymentMode mode) =>
        mode switch
        {
            CreditReceivedPaymentMode.Card => (PaymentProviderKind.PineLabs, "Card"),
            CreditReceivedPaymentMode.UPI => (PaymentProviderKind.Razorpay, "UPI"),
            CreditReceivedPaymentMode.CreditNote => (PaymentProviderKind.CreditNote, "Credit Note"),
            _ => (PaymentProviderKind.Cash, "Cash"),
        };
}
