using System;
using MongoDB.Bson;
using RRBridal.StoreBilling.App.Services.Store;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public class StoreBillListServiceTests
{
    [Fact]
    public void MatchesDateFilter_single_business_day()
    {
        var utc = new DateTime(2026, 6, 6, 10, 0, 0, DateTimeKind.Utc);
        var doc = new BsonDocument { { "createdAtUtc", utc.ToString("O") } };
        var localDay = utc.ToLocalTime().Date;

        var query = new StoreBillListQuery { BusinessDate = localDay, UseDateRange = false };
        Assert.True(StoreBillListService.MatchesDateFilter(doc, query));
        Assert.False(StoreBillListService.MatchesDateFilter(doc, new StoreBillListQuery
        {
            BusinessDate = localDay.AddDays(1),
            UseDateRange = false,
        }));
    }

    [Fact]
    public void MatchesDateFilter_date_range()
    {
        var utc = new DateTime(2026, 6, 6, 10, 0, 0, DateTimeKind.Utc);
        var doc = new BsonDocument { { "createdAtUtc", utc.ToString("O") } };
        var localDay = utc.ToLocalTime().Date;

        var query = new StoreBillListQuery
        {
            UseDateRange = true,
            DateFrom = localDay.AddDays(-1),
            DateTo = localDay.AddDays(1),
        };
        Assert.True(StoreBillListService.MatchesDateFilter(doc, query));

        query = new StoreBillListQuery
        {
            UseDateRange = true,
            DateFrom = localDay.AddDays(2),
            DateTo = localDay.AddDays(3),
        };
        Assert.False(StoreBillListService.MatchesDateFilter(doc, query));
    }

    [Fact]
    public void MatchesCustomerMobile_normalizes_digits()
    {
        var doc = new BsonDocument { { "customerPhone", "9876543210" } };
        Assert.True(StoreBillListService.MatchesCustomerMobile(doc, "9876543210"));
        Assert.True(StoreBillListService.MatchesCustomerMobile(doc, "+91 98765 43210"));
        Assert.False(StoreBillListService.MatchesCustomerMobile(doc, "9123456789"));
    }

    [Fact]
    public void MatchesInvoiceNo_partial_case_insensitive()
    {
        var doc = new BsonDocument { { "billNo", "20260606-001-1-0001" } };
        Assert.True(StoreBillListService.MatchesInvoiceNo(doc, "0001"));
        Assert.True(StoreBillListService.MatchesInvoiceNo(doc, "20260606"));
        Assert.False(StoreBillListService.MatchesInvoiceNo(doc, "9999"));
    }

    [Fact]
    public void BillListCsvExporter_writes_header_and_row()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"bill-list-{Guid.NewGuid():N}.csv");
        try
        {
            var rows = new[]
            {
                new StoreBillListRow
                {
                    BillNo = "B-1",
                    PostedAtLocal = "06-Jun-2026",
                    Payable = 100m,
                    CashAmount = 100m,
                    SyncStatus = "Synced",
                },
            };
            BillListCsvExporter.ExportToFile(path, rows);
            var text = System.IO.File.ReadAllText(path);
            Assert.Contains("Bill no", text);
            Assert.Contains("B-1", text);
            Assert.Contains("100.00", text);
        }
        finally
        {
            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
        }
    }
}
