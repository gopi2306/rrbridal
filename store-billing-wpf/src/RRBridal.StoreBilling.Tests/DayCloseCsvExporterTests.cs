using System;
using System.IO;
using System.Linq;
using System.Text;
using MongoDB.Bson;
using RRBridal.StoreBilling.App.Services.Store;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public class DayCloseCsvExporterTests
{
    [Fact]
    public void BuildContent_includes_sections_summary_and_utf8_bom()
    {
        var data = SampleReportData();
        var content = DayCloseCsvExporter.BuildContent(data);

        Assert.StartsWith("\uFEFF", content);
        Assert.Contains("--- METADATA ---", content);
        Assert.Contains("--- SUMMARY ---", content);
        Assert.Contains("Expected cash (drawer)", content);
        Assert.Contains("--- BILLS ---", content);
        Assert.Contains("--- RETURNS ---", content);
        Assert.Contains("CN-2024-001", content);
        Assert.Contains("20240617-001-1-1", content);
    }

    [Fact]
    public void BuildContent_store_wide_includes_overall_then_pos_summaries()
    {
        var data = SampleStoreWideReportData();
        var content = DayCloseCsvExporter.BuildContent(data);

        Assert.Contains("--- SUMMARY_OVERALL ---", content);
        Assert.Contains("--- SUMMARY_POS1 ---", content);
        Assert.Contains("--- SUMMARY_POS2 ---", content);
        Assert.DoesNotContain("--- SUMMARY ---", content);
        var overallIdx = content.IndexOf("--- SUMMARY_OVERALL ---", StringComparison.Ordinal);
        var pos1Idx = content.IndexOf("--- SUMMARY_POS1 ---", StringComparison.Ordinal);
        var pos2Idx = content.IndexOf("--- SUMMARY_POS2 ---", StringComparison.Ordinal);
        Assert.True(overallIdx >= 0 && pos1Idx > overallIdx && pos2Idx > pos1Idx);
    }

    [Fact]
    public void BuildContent_escapes_commas_in_fields()
    {
        var data = SampleReportData("Store, Main");
        var content = DayCloseCsvExporter.BuildContent(data);
        Assert.Contains("\"Store, Main (store-001)\"", content);
    }

    [Fact]
    public void ExportToFile_writes_utf8_bom_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"day-close-test-{Guid.NewGuid():N}.csv");
        try
        {
            DayCloseCsvExporter.ExportToFile(path, SampleReportData());
            var bytes = File.ReadAllBytes(path);
            Assert.Equal(0xEF, bytes[0]);
            Assert.Equal(0xBB, bytes[1]);
            Assert.Equal(0xBF, bytes[2]);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void ExcelExporter_creates_expected_worksheets()
    {
        using var workbook = DayCloseExcelExporter.BuildWorkbook(SampleReportData());
        Assert.Contains(workbook.Worksheets, ws => ws.Name == "METADATA");
        Assert.Contains(workbook.Worksheets, ws => ws.Name == "SUMMARY");
        Assert.Contains(workbook.Worksheets, ws => ws.Name == "BILLS");
        Assert.Contains(workbook.Worksheets, ws => ws.Name == "RETURNS");
    }

    [Fact]
    public void ExcelExporter_store_wide_creates_overall_then_pos_sheets()
    {
        using var workbook = DayCloseExcelExporter.BuildWorkbook(SampleStoreWideReportData());
        Assert.Contains(workbook.Worksheets, ws => ws.Name == "SUMMARY_OVERALL");
        Assert.Contains(workbook.Worksheets, ws => ws.Name == "SUMMARY_POS1");
        Assert.Contains(workbook.Worksheets, ws => ws.Name == "SUMMARY_POS2");
        Assert.DoesNotContain(workbook.Worksheets, ws => ws.Name == "SUMMARY");

        var names = workbook.Worksheets.Select(ws => ws.Name).ToList();
        Assert.True(names.IndexOf("SUMMARY_OVERALL") < names.IndexOf("SUMMARY_POS1"));
        Assert.True(names.IndexOf("SUMMARY_POS1") < names.IndexOf("SUMMARY_POS2"));
    }

    private static DayCloseReportData SampleStoreWideReportData()
    {
        var overall = new DayBillingCloseSnapshot
        {
            LocalDate = new DateTime(2024, 6, 17),
            BillCount = 3,
            TotalQty = 3m,
            TotalAmount = 10000m,
            CashTotal = 9000m,
            OpeningCash = 300m,
            ExpectedCash = 9300m,
            NetCashInHand = 9000m,
            NetCardInHand = 500m,
            NetUpiInHand = 500m,
            ActualHandInTotal = 10000m,
        };

        var pos1 = new DayBillingCloseSnapshot
        {
            LocalDate = new DateTime(2024, 6, 17),
            BillCount = 1,
            CashTotal = 4000m,
            OpeningCash = 100m,
            ExpectedCash = 4100m,
            NetCashInHand = 4000m,
        };

        var pos2 = new DayBillingCloseSnapshot
        {
            LocalDate = new DateTime(2024, 6, 17),
            BillCount = 2,
            CashTotal = 5000m,
            OpeningCash = 200m,
            ExpectedCash = 5200m,
            NetCashInHand = 5000m,
        };

        return new DayCloseReportData
        {
            Metadata = new DayCloseReportMetadata
            {
                StoreId = "store-001",
                StoreName = "RRSTYLE LLP",
                BusinessDate = "2024-06-17",
                CounterScope = "All counters",
                SessionStatus = "closed",
                ExportedAtLocal = "17-Jun-2024 18:00",
            },
            Snapshot = overall,
            CounterSummaries =
            [
                new DayCloseCounterSummary
                {
                    PosCounter = "1",
                    CounterDisplay = "POS1",
                    Snapshot = pos1,
                    SessionStatus = DaySessionStatus.Closed,
                },
                new DayCloseCounterSummary
                {
                    PosCounter = "2",
                    CounterDisplay = "POS2",
                    Snapshot = pos2,
                    SessionStatus = DaySessionStatus.Closed,
                },
            ],
            StoreRollup = new StoreDaySessionRollup
            {
                BusinessDate = "2024-06-17",
                Counters =
                [
                    new DaySessionRollupRow
                    {
                        PosCounter = "1",
                        Status = DaySessionStatus.Closed,
                        OpeningCash = 100m,
                        ExpectedCash = 4100m,
                    },
                    new DaySessionRollupRow
                    {
                        PosCounter = "2",
                        Status = DaySessionStatus.Closed,
                        OpeningCash = 200m,
                        ExpectedCash = 5200m,
                    },
                ],
            },
        };
    }

    private static DayCloseReportData SampleReportData(string storeName = "RRSTYLE LLP")
    {
        var snapshot = new DayBillingCloseSnapshot
        {
            LocalDate = new DateTime(2024, 6, 17),
            BillCount = 1,
            TotalQty = 1m,
            TotalAmount = 7549m,
            CashTotal = 7549m,
            OpeningCash = 100m,
            ExpectedCash = 7649m,
            ActualCashCounted = 7650m,
            CashDifference = 1m,
            NetCashInHand = 7549m,
            NetCardInHand = 0m,
            NetUpiInHand = 0m,
            ActualHandInTotal = 7549m,
        };

        return new DayCloseReportData
        {
            Metadata = new DayCloseReportMetadata
            {
                StoreId = "store-001",
                StoreName = storeName,
                BusinessDate = "2024-06-17",
                CounterScope = "POS1",
                SessionStatus = "closed",
                ExportedAtLocal = "17-Jun-2024 18:00",
            },
            Snapshot = snapshot,
            StoreRollup = new StoreDaySessionRollup
            {
                BusinessDate = "2024-06-17",
                Counters =
                [
                    new DaySessionRollupRow
                    {
                        PosCounter = "1",
                        Status = DaySessionStatus.Closed,
                        OpeningCash = 100m,
                        ExpectedCash = 7649m,
                        ActualCashCounted = 7650m,
                        CashDifference = 1m,
                        ClosedBy = "Admin",
                    },
                ],
            },
            Bills =
            [
                new StoreBillListRow
                {
                    BillNo = "20240617-001-1-1",
                    PostedAtLocal = "17-Jun-2024 01:09",
                    Payable = 7549m,
                    CashAmount = 7549m,
                    CreditNoteRefs = "CN-REF-1",
                    SyncStatus = "Synced",
                },
            ],
            Returns =
            [
                new DayCloseReportReturnRow
                {
                    ReturnNo = "R-001",
                    OriginalBillNo = "B-OLD",
                    PostedAtLocal = "17-Jun-2024 02:00",
                    ReturnTotal = 100m,
                    CreditNoteNo = "CN-2024-001",
                },
            ],
        };
    }
}
