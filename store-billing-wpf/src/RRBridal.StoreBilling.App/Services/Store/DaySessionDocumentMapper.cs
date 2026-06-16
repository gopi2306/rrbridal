using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Bson;

namespace RRBridal.StoreBilling.App.Services.Store;

internal static class DaySessionDocumentMapper
{
    public static DaySessionRecord? ToRecord(BsonDocument? doc)
    {
        if (doc == null)
            return null;

        var denominations = new List<CashDenominationLine>();
        if (doc.TryGetValue("cashDenominations", out var denVal) && denVal.IsBsonArray)
        {
            foreach (BsonDocument line in denVal.AsBsonArray.OfType<BsonDocument>())
            {
                denominations.Add(new CashDenominationLine
                {
                    Denomination = (int)DayBillingCloseDocumentReader.ReadDecimal(line, "denomination"),
                    UnitCount = (int)DayBillingCloseDocumentReader.ReadDecimal(line, "unitCount"),
                });
            }
        }

        DayBillingCloseSnapshot? closeSnapshot = null;
        if (doc.TryGetValue("closeSnapshot", out var snapVal) && snapVal.IsBsonDocument)
            closeSnapshot = ParseCloseSnapshot(snapVal.AsBsonDocument);

        return new DaySessionRecord
        {
            SessionId = DayBillingCloseDocumentReader.ReadString(doc, "sessionId") ?? "",
            StoreId = DayBillingCloseDocumentReader.ReadString(doc, "storeId") ?? "",
            PosCounter = DayBillingCloseDocumentReader.ReadString(doc, "posCounter") ?? "",
            DeviceId = DayBillingCloseDocumentReader.ReadString(doc, "deviceId") ?? "",
            BusinessDate = DayBillingCloseDocumentReader.ReadString(doc, "businessDate") ?? "",
            Status = DayBillingCloseDocumentReader.ReadString(doc, "status") ?? DaySessionStatus.Open,
            OpeningCash = DayBillingCloseDocumentReader.ReadDecimal(doc, "openingCash"),
            ExpectedCash = DayBillingCloseDocumentReader.ReadDecimal(doc, "expectedCash"),
            ActualCashCounted = DayBillingCloseDocumentReader.ReadDecimal(doc, "actualCashCounted"),
            CashDifference = DayBillingCloseDocumentReader.ReadDecimal(doc, "cashDifference"),
            CashDenominations = denominations,
            CashHandOverPrintedAtUtc = DayBillingCloseDocumentReader.ReadString(doc, "cashHandOverPrintedAtUtc"),
            OpenedBy = DayBillingCloseDocumentReader.ReadString(doc, "openedBy"),
            OpenedAtUtc = DayBillingCloseDocumentReader.ReadString(doc, "openedAtUtc"),
            ClosedBy = DayBillingCloseDocumentReader.ReadString(doc, "closedBy"),
            ClosedAtUtc = DayBillingCloseDocumentReader.ReadString(doc, "closedAtUtc"),
            Notes = DayBillingCloseDocumentReader.ReadString(doc, "notes"),
            CashTaken = DayBillingCloseDocumentReader.ReadString(doc, "cashTaken"),
            CloseSnapshot = closeSnapshot,
        };
    }

    public static CashMovementRecord ToMovementRecord(BsonDocument doc) => new()
    {
        MovementNo = DayBillingCloseDocumentReader.ReadString(doc, "movementNo") ?? "",
        MovementType = DayBillingCloseDocumentReader.ReadString(doc, "movementType") ?? "",
        Description = DayBillingCloseDocumentReader.ReadString(doc, "description") ?? "",
        BusinessDate = DayBillingCloseDocumentReader.ReadString(doc, "businessDate") ?? "",
        Amount = DayBillingCloseDocumentReader.ReadDecimal(doc, "amount"),
        Status = DayBillingCloseDocumentReader.ReadString(doc, "status") ?? "posted",
        StoreId = DayBillingCloseDocumentReader.ReadString(doc, "storeId") ?? "",
        PosCounter = DayBillingCloseDocumentReader.ReadString(doc, "posCounter") ?? "",
        DeviceId = DayBillingCloseDocumentReader.ReadString(doc, "deviceId") ?? "",
        CreatedAtUtc = DayBillingCloseDocumentReader.ReadString(doc, "createdAtUtc") ?? "",
    };

    public static BsonArray ToDenominationArray(IReadOnlyList<CashDenominationLine> lines)
    {
        var arr = new BsonArray();
        foreach (var line in lines)
        {
            arr.Add(new BsonDocument
            {
                { "denomination", line.Denomination },
                { "unitCount", line.UnitCount },
                { "amount", (double)line.Amount },
            });
        }

        return arr;
    }

    public static BsonDocument ToCloseSnapshotDocument(DayBillingCloseSnapshot snap) => new()
    {
        { "localDate", snap.LocalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
        { "billCount", snap.BillCount },
        { "totalQty", (double)snap.TotalQty },
        { "totalAmount", (double)snap.TotalAmount },
        { "cashTotal", (double)snap.CashTotal },
        { "cardTotal", (double)snap.CardTotal },
        { "upiTotal", (double)snap.UpiTotal },
        { "creditNoteTotal", (double)snap.CreditNoteTotal },
        { "returnCount", snap.ReturnCount },
        { "returnTotalAmount", (double)snap.ReturnTotalAmount },
        { "cashRefundTotal", (double)snap.CashRefundTotal },
        { "netCashInHand", (double)snap.NetCashInHand },
        { "netCardInHand", (double)snap.NetCardInHand },
        { "netUpiInHand", (double)snap.NetUpiInHand },
        { "actualHandInTotal", (double)snap.ActualHandInTotal },
        { "dailyExpensesTotal", (double)snap.DailyExpensesTotal },
        { "depositsTotal", (double)snap.DepositsTotal },
        { "withdrawalsTotal", (double)snap.WithdrawalsTotal },
        { "openingCash", (double)snap.OpeningCash },
        { "expectedCash", (double)snap.ExpectedCash },
        { "actualCashCounted", (double)snap.ActualCashCounted },
        { "cashDifference", (double)snap.CashDifference },
    };

    private static DayBillingCloseSnapshot ParseCloseSnapshot(BsonDocument doc) => new()
    {
        LocalDate = DateTime.TryParse(
            DayBillingCloseDocumentReader.ReadString(doc, "localDate"),
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var d)
            ? d.Date
            : DateTime.Today,
        BillCount = (int)DayBillingCloseDocumentReader.ReadDecimal(doc, "billCount"),
        TotalQty = DayBillingCloseDocumentReader.ReadDecimal(doc, "totalQty"),
        TotalAmount = DayBillingCloseDocumentReader.ReadDecimal(doc, "totalAmount"),
        CashTotal = DayBillingCloseDocumentReader.ReadDecimal(doc, "cashTotal"),
        CardTotal = DayBillingCloseDocumentReader.ReadDecimal(doc, "cardTotal"),
        UpiTotal = DayBillingCloseDocumentReader.ReadDecimal(doc, "upiTotal"),
        CreditNoteTotal = DayBillingCloseDocumentReader.ReadDecimal(doc, "creditNoteTotal"),
        ReturnCount = (int)DayBillingCloseDocumentReader.ReadDecimal(doc, "returnCount"),
        ReturnTotalAmount = DayBillingCloseDocumentReader.ReadDecimal(doc, "returnTotalAmount"),
        CashRefundTotal = DayBillingCloseDocumentReader.ReadDecimal(doc, "cashRefundTotal"),
        NetCashInHand = DayBillingCloseDocumentReader.ReadDecimal(doc, "netCashInHand"),
        NetCardInHand = DayBillingCloseDocumentReader.ReadDecimal(doc, "netCardInHand"),
        NetUpiInHand = DayBillingCloseDocumentReader.ReadDecimal(doc, "netUpiInHand"),
        ActualHandInTotal = DayBillingCloseDocumentReader.ReadDecimal(doc, "actualHandInTotal"),
        DailyExpensesTotal = DayBillingCloseDocumentReader.ReadDecimal(doc, "dailyExpensesTotal"),
        DepositsTotal = DayBillingCloseDocumentReader.ReadDecimal(doc, "depositsTotal"),
        WithdrawalsTotal = DayBillingCloseDocumentReader.ReadDecimal(doc, "withdrawalsTotal"),
        OpeningCash = DayBillingCloseDocumentReader.ReadDecimal(doc, "openingCash"),
        ExpectedCash = DayBillingCloseDocumentReader.ReadDecimal(doc, "expectedCash"),
        ActualCashCounted = DayBillingCloseDocumentReader.ReadDecimal(doc, "actualCashCounted"),
        CashDifference = DayBillingCloseDocumentReader.ReadDecimal(doc, "cashDifference"),
    };
}
