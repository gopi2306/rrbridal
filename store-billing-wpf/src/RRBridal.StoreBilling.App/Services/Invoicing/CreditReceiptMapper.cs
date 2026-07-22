using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Bson;
using RRBridal.StoreBilling.App.Services.Billing;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public static class CreditReceiptMapper
{
    private static readonly CultureInfo In = CultureInfo.GetCultureInfo("en-IN");

    public static CreditReceiptPrintInput FromPostedBill(
        BsonDocument bill,
        StoreProfile store,
        int charWidth,
        string? receivedBy = null)
    {
        var history = ReadPaymentHistory(bill);
        var advance = CreditBillDocumentReader.ReadAdvanceAtPost(bill);
        var amountPaid = CreditBillDocumentReader.ReadAmountPaid(bill);
        var thisTime = history
            .Where(h => string.Equals(h.Kind, "advance", StringComparison.OrdinalIgnoreCase))
            .Sum(h => h.Amount);
        if (thisTime <= 0)
            thisTime = advance;

        var advanceRow = history.FirstOrDefault(h =>
            string.Equals(h.Kind, "advance", StringComparison.OrdinalIgnoreCase));

        return BuildCommon(
            CreditReceiptKind.CreditInvoiceAtPost,
            bill,
            store,
            charWidth,
            receiptNo: string.IsNullOrWhiteSpace(advanceRow?.ReceiptNo) ? null : advanceRow!.ReceiptNo,
            amountPaidThisTime: thisTime,
            cumulativePaid: amountPaid,
            paymentMode: advanceRow?.Mode ?? "Credit",
            reference: advanceRow?.Reference ?? "",
            receivedBy: receivedBy ?? advanceRow?.ReceivedBy ?? "",
            receivedAtDisplay: advanceRow?.ReceivedAtDisplay ?? "",
            history);
    }

    public static CreditReceiptPrintInput FromCollection(
        BsonDocument bill,
        BsonDocument receiptDoc,
        StoreProfile store,
        int charWidth)
    {
        var history = ReadPaymentHistory(bill);
        var receiptNo = ReadString(receiptDoc, "receiptNo") ?? "";
        var amountThis = ReadDecimal(receiptDoc, "amount");
        var mode = ReadString(receiptDoc, "mode") ?? "";
        var reference = ReadString(receiptDoc, "reference") ?? "";
        var receivedBy = ReadString(receiptDoc, "receivedBy") ?? "";
        var createdAt = ReadString(receiptDoc, "createdAtUtc");
        var receivedAtDisplay = FormatUtcDisplay(createdAt);

        return BuildCommon(
            CreditReceiptKind.BalanceCollection,
            bill,
            store,
            charWidth,
            receiptNo: receiptNo,
            amountPaidThisTime: amountThis,
            cumulativePaid: ReadDecimal(receiptDoc, "amountPaid") > 0
                ? ReadDecimal(receiptDoc, "amountPaid")
                : CreditBillDocumentReader.ReadAmountPaid(bill),
            paymentMode: mode,
            reference: reference,
            receivedBy: receivedBy,
            receivedAtDisplay: receivedAtDisplay,
            history,
            balanceOverride: receiptDoc.Contains("balanceDue")
                ? ReadDecimal(receiptDoc, "balanceDue")
                : null);
    }

    public static CreditReceiptPrintInput CreateSample(StoreProfile store, CreditPrintFormat format, int charWidth = 48)
    {
        var history = new List<CreditPaymentHistoryRow>
        {
            new()
            {
                Kind = "advance",
                ReceivedAtDisplay = DateTime.Now.ToString("dd-MMM-yyyy HH:mm", In),
                Amount = 1000m,
                Mode = "Cash",
                Reference = "ADV-001",
                ReceiptNo = "",
                ReceivedBy = "Admin",
            },
            new()
            {
                Kind = "partial",
                ReceivedAtDisplay = DateTime.Now.ToString("dd-MMM-yyyy HH:mm", In),
                Amount = 500m,
                Mode = "UPI",
                Reference = "UPI-123",
                ReceiptNo = "RCPT-SAMPLE-001",
                ReceivedBy = "Admin",
            },
        };

        return new CreditReceiptPrintInput
        {
            Kind = CreditReceiptKind.BalanceCollection,
            Store = store,
            CharWidth = charWidth is >= 32 and <= 56 ? charWidth : 48,
            BillNo = "20260715-001-1-0001",
            BillDate = DateTime.Today.ToString("dd-MMM-yyyy", In),
            ReceiptNo = "RCPT-SAMPLE-001",
            CustomerName = "Sample Customer",
            CustomerPhone = "9876543210",
            CustomerCode = "CUST-0001",
            Salesman = "Salesman",
            Counter = "1",
            Lines =
            [
                new CreditReceiptLine
                {
                    LineNo = 1,
                    Description = "SAMPLE PRODUCT 3PCS SUIT",
                    Qty = 1,
                    Rate = 4149m,
                    Amount = 4149m,
                },
            ],
            TotalPayable = 4149m,
            AdvanceAtPost = 1000m,
            AmountPaidThisTime = 500m,
            CumulativeAmountPaid = 1500m,
            BalanceDue = 2649m,
            Status = CreditBillDocumentReader.StatusPartial,
            PaymentMode = "UPI",
            Reference = "UPI-123",
            ReceivedBy = "Admin",
            ReceivedAtDisplay = DateTime.Now.ToString("dd-MMM-yyyy HH:mm", In),
            PaymentHistory = history,
        };
    }

    private static CreditReceiptPrintInput BuildCommon(
        CreditReceiptKind kind,
        BsonDocument bill,
        StoreProfile store,
        int charWidth,
        string? receiptNo,
        decimal amountPaidThisTime,
        decimal cumulativePaid,
        string paymentMode,
        string reference,
        string receivedBy,
        string receivedAtDisplay,
        IReadOnlyList<CreditPaymentHistoryRow> history,
        decimal? balanceOverride = null)
    {
        var totalPayable = CreditBillDocumentReader.ReadTotalPayable(bill);
        var balance = balanceOverride
            ?? CreditBillDocumentReader.ReadBalanceDue(bill);

        return new CreditReceiptPrintInput
        {
            Kind = kind,
            Store = store,
            CharWidth = charWidth is >= 32 and <= 56 ? charWidth : 48,
            BillNo = ReadString(bill, "billNo") ?? "",
            BillDate = ReadString(bill, "billDate") ?? "",
            ReceiptNo = receiptNo,
            CustomerName = ReadString(bill, "customerName") ?? "",
            CustomerPhone = ReadString(bill, "customerPhone") ?? "",
            CustomerCode = ReadString(bill, "customerCode") ?? "",
            Salesman = FormatSalesman(ReadString(bill, "salesmanCode"), ReadString(bill, "salesman")),
            Counter = ReadString(bill, "posCounter") ?? "",
            Lines = ReadLines(bill),
            TotalPayable = totalPayable,
            AdvanceAtPost = CreditBillDocumentReader.ReadAdvanceAtPost(bill),
            AmountPaidThisTime = amountPaidThisTime,
            CumulativeAmountPaid = cumulativePaid,
            BalanceDue = balance,
            Status = CreditBillDocumentReader.ReadStatus(bill),
            PaymentMode = paymentMode,
            Reference = reference,
            ReceivedBy = receivedBy,
            ReceivedAtDisplay = receivedAtDisplay,
            PaymentHistory = history,
        };
    }

    private static IReadOnlyList<CreditReceiptLine> ReadLines(BsonDocument bill)
    {
        var lines = new List<CreditReceiptLine>();
        if (!bill.TryGetValue("lines", out var linesVal) || !linesVal.IsBsonArray)
            return lines;

        foreach (BsonDocument line in linesVal.AsBsonArray.OfType<BsonDocument>())
        {
            var qty = ReadDecimal(line, "qty");
            var amount = ReadDecimal(line, "revisedInclusiveAmount");
            if (amount <= 0)
                amount = ReadDecimal(line, "amount");
            if (qty <= 0 && amount <= 0)
                continue;

            lines.Add(new CreditReceiptLine
            {
                LineNo = line.GetValue("lineNo", 0).ToInt32(),
                Description = ReadString(line, "description") ?? "",
                Qty = qty,
                Rate = ReadDecimal(line, "rate"),
                Amount = amount,
            });
        }

        return lines;
    }

    private static IReadOnlyList<CreditPaymentHistoryRow> ReadPaymentHistory(BsonDocument bill)
    {
        var cb = CreditBillDocumentReader.GetCreditBilling(bill);
        if (cb == null || !cb.TryGetValue("payments", out var paymentsVal) || !paymentsVal.IsBsonArray)
            return Array.Empty<CreditPaymentHistoryRow>();

        var rows = new List<CreditPaymentHistoryRow>();
        foreach (BsonDocument p in paymentsVal.AsBsonArray.OfType<BsonDocument>())
        {
            rows.Add(new CreditPaymentHistoryRow
            {
                Kind = ReadString(p, "kind") ?? "",
                ReceivedAtDisplay = FormatUtcDisplay(ReadString(p, "receivedAtUtc")),
                Amount = ReadDecimal(p, "amount"),
                Mode = ReadString(p, "mode") ?? "",
                Reference = ReadString(p, "reference") ?? "",
                ReceiptNo = ReadString(p, "receiptNo") ?? "",
                ReceivedBy = ReadString(p, "receivedBy") ?? "",
            });
        }

        return rows;
    }

    private static string FormatUtcDisplay(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso))
            return "";
        if (!DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            return iso;
        var local = dt.Kind == DateTimeKind.Utc ? dt.ToLocalTime() : dt;
        return local.ToString("dd-MMM-yyyy HH:mm", In);
    }

    private static string FormatSalesman(string? code, string? name)
    {
        code = code?.Trim() ?? "";
        name = name?.Trim() ?? "";
        if (string.IsNullOrEmpty(code))
            return name;
        if (string.IsNullOrEmpty(name))
            return code;
        return $"{code} — {name}";
    }

    private static string? ReadString(BsonDocument d, string key) =>
        d.TryGetValue(key, out var v) && !v.IsBsonNull
            ? v.IsString ? v.AsString : v.ToString()
            : null;

    private static decimal ReadDecimal(BsonDocument d, string key)
    {
        if (!d.TryGetValue(key, out var v) || v.IsBsonNull)
            return 0m;
        return v switch
        {
            { IsDouble: true } => (decimal)v.AsDouble,
            { IsInt32: true } => v.AsInt32,
            { IsInt64: true } => v.AsInt64,
            { IsDecimal128: true } => (decimal)v.AsDecimal128,
            { IsString: true } => decimal.TryParse(v.AsString, NumberStyles.Any, CultureInfo.InvariantCulture, out var d2) ? d2 : 0m,
            _ => 0m,
        };
    }
}
