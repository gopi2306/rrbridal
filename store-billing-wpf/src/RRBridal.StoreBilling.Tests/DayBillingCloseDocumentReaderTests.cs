using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using RRBridal.StoreBilling.App.Services.Store;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public class DayBillingCloseDocumentReaderTests
{
    [Fact]
    public void MatchesLocalDay_uses_local_timezone()
    {
        var localDate = new DateTime(2026, 6, 4);
        var utcEvening = new DateTime(2026, 6, 4, 18, 30, 0, DateTimeKind.Utc);
        var doc = new BsonDocument
        {
            { "createdAtUtc", utcEvening.ToString("O") },
        };

        var localDay = utcEvening.ToLocalTime().Date;
        Assert.True(DayBillingCloseDocumentReader.MatchesLocalDay(doc, localDay));
        Assert.False(DayBillingCloseDocumentReader.MatchesLocalDay(doc, localDay.AddDays(1)));
    }

    [Fact]
    public void SumBillPayments_aggregates_split_payments()
    {
        var doc = BsonDocument.Parse(
            """
            {
              "payments": [
                { "provider": "Cash", "amount": 1000 },
                { "provider": "PineLabs", "amount": 500 },
                { "provider": "Razorpay", "amount": 250 },
                { "provider": "CreditNote", "amount": 100 }
              ]
            }
            """);

        var totals = DayBillingCloseDocumentReader.SumBillPayments(doc);

        Assert.Equal(1000m, totals.Cash);
        Assert.Equal(500m, totals.Card);
        Assert.Equal(250m, totals.Upi);
        Assert.Equal(100m, totals.CreditNote);
    }

    [Fact]
    public void SumBillLineQty_skips_zero_amount_lines()
    {
        var doc = BsonDocument.Parse(
            """
            {
              "lines": [
                { "qty": 2, "amount": 100, "revisedAmount": 95 },
                { "qty": 5, "amount": 0, "revisedAmount": 0 }
              ]
            }
            """);

        Assert.Equal(2m, DayBillingCloseDocumentReader.SumBillLineQty(doc));
    }

    [Fact]
    public void ResolveSyncStatus_maps_outbox_states()
    {
        var bill = new BsonDocument { { "billNo", "B-001" } };
        var synced = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["B-001"] = "synced",
        };
        var pending = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["B-001"] = "pending",
        };

        Assert.Equal("Synced", DayBillingCloseDocumentReader.ResolveSyncStatus(bill, synced));
        Assert.Equal("Pending sync", DayBillingCloseDocumentReader.ResolveSyncStatus(bill, pending));
    }

    [Fact]
    public void ResolveSyncStatus_not_queued_when_postWarnings_mention_outbox()
    {
        var bill = BsonDocument.Parse(
            """
            {
              "billNo": "B-002",
              "postWarnings": ["Outbox enqueue failed: timeout"]
            }
            """);

        Assert.Equal("Not queued", DayBillingCloseDocumentReader.ResolveSyncStatus(bill, new Dictionary<string, string>()));
    }

    [Fact]
    public void BuildOutboxSyncByBillNo_prefers_synced_over_pending()
    {
        var events = new[]
        {
            BsonDocument.Parse("""{ "type": "InvoiceCreated", "status": "pending", "payload": { "billNo": "X-1" } }"""),
            BsonDocument.Parse("""{ "type": "InvoiceCreated", "status": "synced", "payload": { "billNo": "X-1" } }"""),
        };

        var map = DayBillingCloseDocumentReader.BuildOutboxSyncByBillNo(events);

        Assert.Equal("synced", map["X-1"]);
    }

    [Fact]
    public void IsPostedBill_treats_missing_status_as_posted()
    {
        Assert.True(DayBillingCloseDocumentReader.IsPostedBill(new BsonDocument()));
        Assert.False(DayBillingCloseDocumentReader.IsPostedBill(new BsonDocument { { "status", "draft" } }));
    }

    [Fact]
    public void IsPostedReturn_treats_missing_status_as_posted()
    {
        Assert.True(DayBillingCloseDocumentReader.IsPostedReturn(new BsonDocument()));
        Assert.False(DayBillingCloseDocumentReader.IsPostedReturn(new BsonDocument { { "status", "draft" } }));
    }

    [Fact]
    public void AggregateReturnDayTotals_cash_refund_reduces_net_cash_only()
    {
        var returns = new[]
        {
            BsonDocument.Parse(
                """
                {
                  "status": "posted",
                  "returnTotal": 3000,
                  "creditBalance": 3000,
                  "returnMode": "cash_refund",
                  "amountCollected": 0
                }
                """),
        };

        var totals = DayBillingCloseDocumentReader.AggregateReturnDayTotals(returns);

        Assert.Equal(1, totals.ReturnCount);
        Assert.Equal(3000m, totals.ReturnTotalAmount);
        Assert.Equal(3000m, totals.CashRefundTotal);
        Assert.Equal(0m, totals.CreditNoteIssuedTotal);
        Assert.Equal(0m, totals.ExchangePayments.Cash);

        var billCash = 10000m;
        var netCash = billCash - totals.CashRefundTotal + totals.ExchangePayments.Cash;
        Assert.Equal(7000m, netCash);
        Assert.Equal(7000m, netCash + 0m + 0m);
    }

    [Fact]
    public void AggregateReturnDayTotals_credit_note_does_not_reduce_tender()
    {
        var returns = new[]
        {
            BsonDocument.Parse(
                """
                {
                  "status": "posted",
                  "returnTotal": 2500,
                  "creditBalance": 2500,
                  "returnMode": "credit_note",
                  "amountCollected": 0
                }
                """),
        };

        var totals = DayBillingCloseDocumentReader.AggregateReturnDayTotals(returns);

        Assert.Equal(2500m, totals.ReturnTotalAmount);
        Assert.Equal(0m, totals.CashRefundTotal);
        Assert.Equal(2500m, totals.CreditNoteIssuedTotal);
        Assert.Equal(0m, totals.ExchangePayments.Cash);
    }

    [Fact]
    public void AggregateReturnDayTotals_exchange_split_payments_add_to_modes()
    {
        var returns = new[]
        {
            BsonDocument.Parse(
                """
                {
                  "status": "posted",
                  "returnTotal": 3000,
                  "creditBalance": 0,
                  "returnMode": "credit_note",
                  "amountCollected": 2000,
                  "payments": [
                    { "provider": "Cash", "amount": 1000 },
                    { "provider": "PineLabs", "amount": 500 },
                    { "provider": "Razorpay", "amount": 500 }
                  ]
                }
                """),
        };

        var totals = DayBillingCloseDocumentReader.AggregateReturnDayTotals(returns);

        Assert.Equal(3000m, totals.ReturnTotalAmount);
        Assert.Equal(0m, totals.CashRefundTotal);
        Assert.Equal(0m, totals.CreditNoteIssuedTotal);
        Assert.Equal(1000m, totals.ExchangePayments.Cash);
        Assert.Equal(500m, totals.ExchangePayments.Card);
        Assert.Equal(500m, totals.ExchangePayments.Upi);

        var billCash = 5000m;
        var billCard = 1000m;
        var billUpi = 500m;
        var netCash = billCash - totals.CashRefundTotal + totals.ExchangePayments.Cash;
        var netCard = billCard + totals.ExchangePayments.Card;
        var netUpi = billUpi + totals.ExchangePayments.Upi;
        var actualHandIn = netCash + netCard + netUpi;

        Assert.Equal(6000m, netCash);
        Assert.Equal(1500m, netCard);
        Assert.Equal(1000m, netUpi);
        Assert.Equal(8500m, actualHandIn);
    }

    [Fact]
    public void MatchesPosCounterFilter_excludes_other_counters_on_returns()
    {
        var doc = new BsonDocument { { "posCounter", "counter-01" } };

        Assert.True(DayBillingCloseDocumentReader.MatchesPosCounterFilter(doc, null));
        Assert.True(DayBillingCloseDocumentReader.MatchesPosCounterFilter(doc, "counter-01"));
        Assert.False(DayBillingCloseDocumentReader.MatchesPosCounterFilter(doc, "counter-02"));
    }

    [Fact]
    public void FormatReturnPaymentSummary_lists_exchange_legs()
    {
        var doc = BsonDocument.Parse(
            """
            {
              "amountCollected": 1500,
              "payments": [
                { "provider": "Cash", "amount": 1000 },
                { "provider": "Razorpay", "amount": 500 }
              ]
            }
            """);

        var summary = DayBillingCloseDocumentReader.FormatReturnPaymentSummary(doc);

        Assert.Contains("Cash", summary);
        Assert.Contains("UPI", summary);
    }

    [Fact]
    public void AggregateReturnDayTotals_skips_non_posted_returns()
    {
        var returns = new[]
        {
            BsonDocument.Parse("""{ "status": "draft", "returnTotal": 999, "creditBalance": 999, "returnMode": "cash_refund" }"""),
            BsonDocument.Parse("""{ "status": "posted", "returnTotal": 100, "creditBalance": 100, "returnMode": "cash_refund" }"""),
        };

        var totals = DayBillingCloseDocumentReader.AggregateReturnDayTotals(returns);

        Assert.Equal(1, totals.ReturnCount);
        Assert.Equal(100m, totals.ReturnTotalAmount);
        Assert.Equal(100m, totals.CashRefundTotal);
    }

    [Fact]
    public void AggregateReturnDayTotals_prefers_explicit_cashRefunded()
    {
        var returns = new[]
        {
            BsonDocument.Parse(
                """
                {
                  "status": "posted",
                  "returnTotal": 5000,
                  "creditBalance": 5000,
                  "cashRefunded": 4200,
                  "returnMode": "cash_refund"
                }
                """),
        };

        var totals = DayBillingCloseDocumentReader.AggregateReturnDayTotals(returns);

        Assert.Equal(4200m, totals.CashRefundTotal);
    }

    [Fact]
    public void AggregateCreditNoteCashoutDayTotals_sums_posted_cashouts()
    {
        var cashouts = new[]
        {
            BsonDocument.Parse("""{ "status": "posted", "cashRefunded": 500 }"""),
            BsonDocument.Parse("""{ "status": "posted", "cashRefunded": 250.50 }"""),
            BsonDocument.Parse("""{ "status": "draft", "cashRefunded": 999 }"""),
        };

        var total = DayBillingCloseDocumentReader.AggregateCreditNoteCashoutDayTotals(cashouts);

        Assert.Equal(750.50m, total);
    }

    [Fact]
    public void SumDailyExpensesForBusinessDate_filters_by_date_status_and_counter()
    {
        var expenses = new[]
        {
            BsonDocument.Parse("""{ "businessDate": "2026-06-11", "amount": 200, "status": "posted", "posCounter": "1" }"""),
            BsonDocument.Parse("""{ "businessDate": "2026-06-11", "amount": 50, "status": "posted", "posCounter": "2" }"""),
            BsonDocument.Parse("""{ "businessDate": "2026-06-10", "amount": 999, "status": "posted", "posCounter": "1" }"""),
            BsonDocument.Parse("""{ "businessDate": "2026-06-11", "amount": 100, "status": "void", "posCounter": "1" }"""),
        };

        Assert.Equal(200m, DayBillingCloseDocumentReader.SumDailyExpensesForBusinessDate(expenses, "2026-06-11", "1"));
        Assert.Equal(250m, DayBillingCloseDocumentReader.SumDailyExpensesForBusinessDate(expenses, "2026-06-11", null));
    }

    [Fact]
    public void SumCashMovementsForBusinessDate_splits_deposits_and_withdrawals()
    {
        var movements = new[]
        {
            BsonDocument.Parse("""{ "businessDate": "2026-06-11", "amount": 1000, "status": "posted", "movementType": "deposit_to_bank", "posCounter": "1" }"""),
            BsonDocument.Parse("""{ "businessDate": "2026-06-11", "amount": 200, "status": "posted", "movementType": "cash_withdrawal", "posCounter": "1" }"""),
            BsonDocument.Parse("""{ "businessDate": "2026-06-11", "amount": 50, "status": "posted", "movementType": "deposit_to_bank", "posCounter": "2" }"""),
        };

        var (deposits, withdrawals) = DayBillingCloseDocumentReader.SumCashMovementsForBusinessDate(
            movements, "2026-06-11", "1");

        Assert.Equal(1000m, deposits);
        Assert.Equal(200m, withdrawals);
    }

    [Fact]
    public void DaySessionCashMath_computes_expected_cash()
    {
        var expected = DaySessionCashMath.ComputeExpectedCash(
            openingCash: 5000m,
            netCashInHand: 45100m,
            depositsTotal: 1000m,
            withdrawalsTotal: 200m);

        Assert.Equal(48900m, expected);
    }

    [Fact]
    public void CashDenominationDefaults_validate_and_sum()
    {
        var lines = new[]
        {
            new CashDenominationLine { Denomination = 500, UnitCount = 84 },
            new CashDenominationLine { Denomination = 100, UnitCount = 11 },
            new CashDenominationLine { Denomination = 1, UnitCount = 1495 },
        };

        var total = CashDenominationDefaults.SumDenominations(lines);
        Assert.Equal(44595m, total);
        Assert.True(CashDenominationDefaults.ValidateDenominations(lines, total, out _));
    }

    [Fact]
    public void FormatBillCreditNoteReferences_joins_payment_references()
    {
        var doc = BsonDocument.Parse(
            """
            {
              "payments": [
                { "provider": "Cash", "amount": 100 },
                { "provider": "CreditNote", "amount": 50, "reference": "CN-001" },
                { "provider": "CreditNote", "amount": 25, "reference": "CN-002" }
              ]
            }
            """);

        Assert.Equal("CN-001; CN-002", DayBillingCloseDocumentReader.FormatBillCreditNoteReferences(doc));
    }

    [Fact]
    public void FilterAdjustmentsForLocalDay_filters_by_date_and_counter()
    {
        var localDate = new DateTime(2024, 6, 17);
        var adjustments = new[]
        {
            new BsonDocument
            {
                { "status", "posted" },
                { "createdAtUtc", new DateTime(2024, 6, 17, 10, 0, 0, DateTimeKind.Utc).ToString("O") },
                { "posCounter", "1" },
                { "adjustmentNo", "ADJ-1" },
            },
            new BsonDocument
            {
                { "status", "posted" },
                { "createdAtUtc", new DateTime(2024, 6, 16, 10, 0, 0, DateTimeKind.Utc).ToString("O") },
                { "posCounter", "1" },
                { "adjustmentNo", "ADJ-2" },
            },
        };

        var filtered = DayBillingCloseDocumentReader.FilterAdjustmentsForLocalDay(adjustments, localDate, "1").ToList();
        Assert.Single(filtered);
        Assert.Equal("ADJ-1", DayBillingCloseDocumentReader.ReadString(filtered[0], "adjustmentNo"));
    }
}
