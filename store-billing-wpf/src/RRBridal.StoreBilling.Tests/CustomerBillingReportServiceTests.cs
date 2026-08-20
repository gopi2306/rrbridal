using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Store;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public sealed class CustomerBillingReportServiceTests
{
    [Fact]
    public void Aggregate_groups_customers_and_applies_returns_and_payment_splits()
    {
        var first = BsonDocument.Parse("""
        {
          "status":"posted","billNo":"B-2","billDate":"2026-08-12","posCounter":"2",
          "customerCode":"cus-1","customerName":"Anu","customerPhone":"98765 43210",
          "payable":90,"creditApplied":10,"payableBeforeCredit":100,
          "payments":[{"provider":"Cash","amount":90},{"provider":"CreditNote","amount":10}],
          "lines":[{"sku":"A","description":"Saree A","qty":2,"rate":50,"amount":100}]
        }
        """);
        var second = BsonDocument.Parse("""
        {
          "status":"posted","billNo":"B-1","billDate":"2026-08-11",
          "customerCode":"CUS-1","customerName":"Anu","customerPhone":"9876543210",
          "payable":50,"paymentMode":"Razorpay","lines":[{"sku":"B","description":"Saree B","qty":1,"rate":50,"amount":50}]
        }
        """);
        var saleReturn = BsonDocument.Parse("""
        {
          "status":"posted","returnNo":"R-1","returnDate":"2026-08-13",
          "originalBillNo":"B-2","returnMode":"credit_note","creditNoteNo":"CN-1",
          "returnLines":[{"sku":"A","returnQty":1,"lineTotal":25}]
        }
        """);

        var result = CustomerBillingReportService.Aggregate(
            [
                (first, new DateTimeOffset(2026, 8, 12, 5, 0, 0, TimeSpan.Zero)),
                (second, new DateTimeOffset(2026, 8, 11, 5, 0, 0, TimeSpan.Zero)),
            ],
            new Dictionary<string, List<BsonDocument>>(StringComparer.Ordinal)
            {
                ["B-2"] = [saleReturn],
            });

        var customer = Assert.Single(result.Data);
        Assert.Equal("code:CUS-1", customer.CustomerKey);
        Assert.Equal(2, customer.BillCount);
        Assert.Equal(3m, customer.Qty);
        Assert.Equal(150m, customer.GrossAmount);
        Assert.Equal(25m, customer.ReturnAmount);
        Assert.Equal(125m, customer.NetAmount);
        Assert.Equal(90m, customer.Payments.Cash);
        Assert.Equal(50m, customer.Payments.Upi);
        Assert.Equal(10m, customer.Payments.CreditNote);
        Assert.Equal("B-2", customer.Bills.First().BillNo);
        Assert.Equal("R-1", Assert.Single(customer.Bills.First().Returns).ReturnNo);
        var line = Assert.Single(customer.Bills.First().Lines);
        Assert.Equal("A", line.Sku);
        Assert.Equal("Saree A", line.Description);
        Assert.Equal(2m, line.Qty);
        Assert.Equal(100m, line.Amount);
    }

    [Fact]
    public void Export_builds_summary_and_bill_details_sheets()
    {
        var report = new CustomerBillingReportResponse
        {
            Period = new CustomerBillingReportPeriod
            {
                From = "2026-08-01",
                To = "2026-08-13",
                StoreCode = "store-001",
                StoreName = "Main Store",
                Timezone = "Asia/Kolkata",
            },
            Totals = new CustomerBillingTotals { CustomerCount = 1, BillCount = 1, NetAmount = 42m },
            Data =
            [
                new CustomerBillingCustomerRow
                {
                    CustomerKey = "walk-in",
                    CustomerName = "Walk-in Customer",
                    BillCount = 1,
                    NetAmount = 42m,
                    Bills =
                    [
                        new CustomerBillingBillDetail
                        {
                            BillNo = "B-1",
                            BillDate = "2026-08-13",
                            CustomerName = "Walk-in Customer",
                            GrossAmount = 50m,
                            ReturnAmount = 8m,
                            NetAmount = 42m,
                            Lines =
                            [
                                new CustomerBillingLineDetail
                                {
                                    LineNo = 1,
                                    Sku = "SKU-1",
                                    Description = "Product 1",
                                    Qty = 1,
                                    Rate = 50,
                                    Amount = 50,
                                },
                            ],
                        },
                    ],
                },
            ],
        };

        using var workbook = CustomerBillingReportExcelExporter.BuildWorkbook(report);

        Assert.Equal(["Summary", "Bill Details", "Line Items"], workbook.Worksheets.Select(sheet => sheet.Name));
        Assert.Contains("Customer-Wise Billing Report", workbook.Worksheet("Summary").CellsUsed()
            .Select(cell => cell.GetString()));
        Assert.Contains("B-1", workbook.Worksheet("Bill Details").CellsUsed()
            .Select(cell => cell.GetString()));
        Assert.Contains("SKU-1", workbook.Worksheet("Line Items").CellsUsed()
            .Select(cell => cell.GetString()));
    }

    [Fact]
    public void Customer_billing_screen_access_defaults_to_admin_and_can_be_granted()
    {
        var access = CounterScreenAccessSettings.CreateDefaults();

        Assert.Contains(nameof(CounterScreenAccessSettings.CustomerBillingReport),
            CounterScreenAccessSettings.AllScreenKeys);
        Assert.True(access.IsCounterAllowed(nameof(CounterScreenAccessSettings.CustomerBillingReport), "1"));
        Assert.False(access.IsCounterAllowed(nameof(CounterScreenAccessSettings.CustomerBillingReport), "2"));

        access.SetAllowed(nameof(CounterScreenAccessSettings.CustomerBillingReport), ["2", "3"]);

        Assert.False(access.IsCounterAllowed(nameof(CounterScreenAccessSettings.CustomerBillingReport), "1"));
        Assert.True(access.IsCounterAllowed(nameof(CounterScreenAccessSettings.CustomerBillingReport), "2"));
    }
}
