using System;
using MongoDB.Bson;
using RRBridal.StoreBilling.App.Services.Expenses;
using RRBridal.StoreBilling.App.Services.Invoicing;
using RRBridal.StoreBilling.App.Services.Store;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public class DailyExpenseDomainTests
{
    [Fact]
    public void CalculateTax_inclusive_intra_state_splits_cgst_and_sgst()
    {
        var tax = DailyExpenseDomain.CalculateTax(1180m, ExpenseGstMode.Inclusive, 18m, ExpenseSupplyType.IntraState);

        Assert.Equal(1000m, tax.TaxableAmount);
        Assert.Equal(90m, tax.CgstAmount);
        Assert.Equal(90m, tax.SgstAmount);
        Assert.Equal(0m, tax.IgstAmount);
        Assert.Equal(1180m, tax.TotalAmount);
    }

    [Fact]
    public void CalculateTax_exclusive_inter_state_uses_igst()
    {
        var tax = DailyExpenseDomain.CalculateTax(1000m, ExpenseGstMode.Exclusive, 18m, ExpenseSupplyType.InterState);

        Assert.Equal(1000m, tax.TaxableAmount);
        Assert.Equal(180m, tax.IgstAmount);
        Assert.Equal(1180m, tax.TotalAmount);
    }

    [Fact]
    public void Validate_requires_balanced_split_payment_legs()
    {
        var draft = new DailyExpenseDraft
        {
            BusinessDate = "2026-08-04",
            Description = "Courier",
            SupplierName = "Vendor",
            SupplierGstin = "33ABCDE1234F1Z5",
            SupplierStateCode = "33",
            StoreStateCode = "33",
            SupplierInvoiceNo = "INV-1",
            SupplierInvoiceDate = "2026-08-04",
            EnteredAmount = 1000m,
            GstMode = ExpenseGstMode.Exclusive,
            GstRate = 18m,
            Payments =
            [
                new DailyExpensePaymentLeg(ExpensePaymentMode.Cash, 500m),
                new DailyExpensePaymentLeg(ExpensePaymentMode.Upi, 600m),
            ],
        };

        var error = DailyExpenseDomain.Validate(draft);

        Assert.NotNull(error);
        Assert.Contains("must equal", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_rejects_invalid_gstin_and_supply_type_mismatch()
    {
        var invalidGstin = new DailyExpenseDraft
        {
            BusinessDate = "2026-08-04",
            Description = "Repairs",
            SupplierName = "Vendor",
            SupplierGstin = "33INVALID",
            SupplierInvoiceNo = "INV-2",
            SupplierInvoiceDate = "2026-08-04",
            EnteredAmount = 1180m,
            GstMode = ExpenseGstMode.Inclusive,
            GstRate = 18m,
            Payments = [new DailyExpensePaymentLeg(ExpensePaymentMode.Cash, 1180m)],
        };
        Assert.Contains("valid", DailyExpenseDomain.Validate(invalidGstin)!, StringComparison.OrdinalIgnoreCase);

        var wrongSupplyType = new DailyExpenseDraft
        {
            BusinessDate = "2026-08-04",
            Description = "Repairs",
            SupplierName = "Vendor",
            SupplierGstin = "29ABCDE1234F1Z5",
            SupplierStateCode = "29",
            StoreStateCode = "33",
            SupplierInvoiceNo = "INV-3",
            SupplierInvoiceDate = "2026-08-04",
            EnteredAmount = 1180m,
            GstMode = ExpenseGstMode.Inclusive,
            GstRate = 18m,
            SupplyType = ExpenseSupplyType.IntraState,
            Payments = [new DailyExpensePaymentLeg(ExpensePaymentMode.Cash, 1180m)],
        };
        Assert.Contains("state codes", DailyExpenseDomain.Validate(wrongSupplyType)!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_allows_gst_expense_without_gstin_or_supplier_invoice_number()
    {
        var draft = new DailyExpenseDraft
        {
            BusinessDate = "2026-08-04",
            Description = "Repairs",
            SupplierName = "Local Vendor",
            EnteredAmount = 1180m,
            GstMode = ExpenseGstMode.Inclusive,
            GstRate = 18m,
            SupplyType = ExpenseSupplyType.IntraState,
            Payments = [new DailyExpensePaymentLeg(ExpensePaymentMode.Cash, 1180m)],
        };

        Assert.Null(DailyExpenseDomain.Validate(draft));
    }

    [Fact]
    public void BuildDocument_preserves_legacy_amount_and_adds_gst_fields()
    {
        var draft = new DailyExpenseDraft
        {
            BusinessDate = "2026-08-04",
            Description = "Packaging",
            Category = "Packaging",
            EnteredAmount = 1000m,
            GstMode = ExpenseGstMode.Exclusive,
            GstRate = 18m,
            Payments = [new DailyExpensePaymentLeg(ExpensePaymentMode.Card, 1180m, "CARD-1")],
        };

        var doc = DailyExpenseDomain.BuildDocument("E-1", "S-1", "D-1", "1", draft, "2026-08-04T10:00:00Z");

        Assert.Equal(1180m, DailyExpenseDomain.ReadDecimal(doc, "amount"));
        Assert.Equal(1000m, DailyExpenseDomain.ReadDecimal(doc, "taxableAmount"));
        Assert.Equal("Card", doc["payments"][0]["mode"].AsString);
    }

    [Fact]
    public void AggregateDailyExpensePayments_uses_legs_and_legacy_cash_fallback_and_excludes_void()
    {
        var docs = new[]
        {
            BsonDocument.Parse("""{ "businessDate": "2026-08-04", "status": "posted", "amount": 100, "posCounter": "1" }"""),
            BsonDocument.Parse("""
                {
                  "businessDate": "2026-08-04", "status": "posted", "amount": 300, "posCounter": "1",
                  "payments": [
                    { "mode": "Cash", "amount": 50 },
                    { "mode": "Card", "amount": 100 },
                    { "mode": "UPI", "amount": 75 },
                    { "mode": "Bank Transfer", "amount": 75 }
                  ]
                }
                """),
            BsonDocument.Parse("""{ "businessDate": "2026-08-04", "status": "void", "amount": 999, "posCounter": "1" }"""),
        };

        var totals = DayBillingCloseDocumentReader.AggregateDailyExpensePayments(docs, "2026-08-04", "1");

        Assert.Equal(400m, totals.Total);
        Assert.Equal(150m, totals.Cash);
        Assert.Equal(100m, totals.Card);
        Assert.Equal(75m, totals.Upi);
        Assert.Equal(75m, totals.BankTransfer);
        Assert.Equal(150m, DayBillingCloseDocumentReader.SumDailyExpensesForBusinessDate(docs, "2026-08-04", "1"));
    }

    [Fact]
    public void ExpenseThermalTextBuilder_prints_gst_and_payment_details()
    {
        var text = ExpenseThermalTextBuilder.Build(new ExpenseThermalInput
        {
            Store = new StoreProfile { StoreName = "RR Bridal" },
            ExpenseNo = "EXP-1",
            BusinessDate = "2026-08-04",
            SupplierName = "Vendor",
            SupplierGstin = "33ABCDE1234F1Z5",
            SupplierInvoiceNo = "INV-1",
            Description = "Repairs",
            TaxableAmount = 1000m,
            CgstAmount = 90m,
            SgstAmount = 90m,
            TotalAmount = 1180m,
            PaymentSummary = "Cash ₹180, Bank Transfer ₹1,000",
        });

        Assert.Contains("EXPENSE VOUCHER", text);
        Assert.Contains("33ABCDE1234F1Z5", text);
        Assert.Contains("CGST:", text);
        Assert.Contains("Bank Transfer", text);
    }
}
