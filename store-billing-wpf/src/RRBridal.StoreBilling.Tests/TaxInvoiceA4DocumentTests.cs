using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Invoicing;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public class TaxInvoiceA4DocumentTests
{
    [Fact]
    public void GstBreakdown_intra_state_splits_cgst_and_sgst()
    {
        var input = SampleBill(isInterState: false);
        var rows = TaxInvoiceA4GstBreakdown.BuildHsnRows(input);
        var totals = TaxInvoiceA4GstBreakdown.ComputeTotals(input);

        var row = Assert.Single(rows);
        Assert.Equal("540710", row.Hsn);
        Assert.Equal(1000m, row.TaxableValue);
        Assert.Equal(2.5m, row.CgstRate);
        Assert.Equal(2.5m, row.SgstRate);
        Assert.Equal(25m, row.CgstAmount);
        Assert.Equal(25m, row.SgstAmount);
        Assert.Equal(50m, row.TotalTax);
        Assert.Equal(25m, totals.Cgst);
        Assert.Equal(25m, totals.Sgst);
        Assert.Equal(0m, totals.Igst);
    }

    [Fact]
    public void GstBreakdown_inter_state_uses_igst()
    {
        var input = SampleBill(isInterState: true);
        var rows = TaxInvoiceA4GstBreakdown.BuildHsnRows(input);
        var totals = TaxInvoiceA4GstBreakdown.ComputeTotals(input);

        var row = Assert.Single(rows);
        Assert.Equal(5m, row.IgstRate);
        Assert.Equal(50m, row.IgstAmount);
        Assert.Equal(0m, row.CgstAmount);
        Assert.Equal(0m, row.SgstAmount);
        Assert.Equal(50m, totals.Igst);
        Assert.Equal(0m, totals.Cgst);
    }

    [Fact]
    public void Document_includes_tax_invoice_cgst_sgst_gst_split_and_bank_labels()
    {
        var text = OnSta(() =>
        {
            var input = SampleBill(
                isInterState: false,
                store: new StoreProfile
                {
                    StoreName = "RR Bridal Test",
                    Address = "Hyderabad",
                    Gstin = "36ABFFR4340C1ZI",
                    StateName = "Telangana",
                    BankAccountHolderName = "RR Bridal Pvt Ltd",
                    BankAccountNumber = "1234567890",
                    BankIfsc = "HDFC0001234",
                    BankBranchName = "Madhapur",
                });
            return CollectDocumentText(TaxInvoiceA4DocumentBuilder.Create(input));
        });

        Assert.Contains("Tax Invoice", text);
        Assert.Contains("CGST", text);
        Assert.Contains("SGST", text);
        Assert.Contains("Central Tax", text);
        Assert.Contains("State Tax", text);
        Assert.Contains("GST breakup", text);
        Assert.Contains("Tax Amount (in words)", text);
        Assert.Contains("Bank Details", text);
        Assert.Contains("Account Holder Name: RR Bridal Pvt Ltd", text);
        Assert.Contains("Bank Account Number: 1234567890", text);
        Assert.Contains("IFSC Code: HDFC0001234", text);
        Assert.Contains("Branch Name: Madhapur", text);
        Assert.Contains("540710", text);
        Assert.Contains("Subtotal", text);
        Assert.Contains("Total", text);
    }

    [Fact]
    public void Document_typical_line_count_stays_single_page_with_totals()
    {
        var (pageCount, text) = OnSta(() =>
        {
            var input = SampleBill(isInterState: false, lineCount: 15);
            var doc = TaxInvoiceA4DocumentBuilder.Create(input);
            return (CountDocumentPages(doc), CollectDocumentText(doc));
        });

        Assert.Equal(1, pageCount);
        Assert.Contains("Subtotal", text);
        Assert.Contains("Total", text);
        Assert.Contains("CGST", text);
        Assert.Contains("SGST", text);
        Assert.Contains("GST breakup", text);
        Assert.DoesNotContain("Tax Invoice (Continued)", text);
    }

    [Fact]
    public void Document_overflow_lines_use_second_page_for_footer_without_meta()
    {
        var (pageCount, text) = OnSta(() =>
        {
            var input = SampleBill(isInterState: false, lineCount: 18);
            var doc = TaxInvoiceA4DocumentBuilder.Create(input);
            return (CountDocumentPages(doc), CollectDocumentText(doc));
        });

        Assert.True(pageCount >= 2, $"Expected multi-page tax invoice for 18 lines, got {pageCount} page(s).");
        Assert.Contains("Subtotal", text);
        Assert.Contains("Total", text);
        Assert.Contains("GST breakup", text);
        Assert.Contains("Tax Invoice (Continued)", text);
    }

    [Fact]
    public void Document_inter_state_shows_igst_not_central_tax()
    {
        var text = OnSta(() =>
        {
            var input = SampleBill(isInterState: true);
            return CollectDocumentText(TaxInvoiceA4DocumentBuilder.Create(input));
        });

        Assert.Contains("Tax Invoice", text);
        Assert.Contains("IGST", text);
        Assert.DoesNotContain("Central Tax", text);
    }

    [Fact]
    public void Factory_routes_a4_tax_invoice_format()
    {
        var text = OnSta(() =>
        {
            var input = SampleBill(isInterState: false);
            var assets = new ThermalReceiptAssets();
            return CollectDocumentText(
                InvoiceDocumentFactory.CreateFullDocument(input, assets, InvoicePrintFormat.A4TaxInvoice));
        });

        Assert.Contains("Tax Invoice", text);
        Assert.Contains("CGST", text);
    }

    [Fact]
    public void ChunkLinesFillThenFooterPage_keeps_single_page_when_footer_fits()
    {
        var lines = Enumerable.Range(1, 10)
            .Select(i => new InvoiceLineSnap
            {
                LineNo = i,
                Description = $"Item {i}",
                Qty = 1,
                Amount = 100,
                TaxableAmount = 95,
            })
            .ToList();

        var chunks = InvoiceLinePagination.ChunkLinesFillThenFooterPage(lines, pageMax: 20, lastPageMax: 15);

        Assert.Single(chunks);
        Assert.Equal(10, chunks[0].Count);
    }

    [Fact]
    public void ChunkLinesFillThenFooterPage_spills_only_when_over_last_page_capacity()
    {
        var lines = Enumerable.Range(1, 18)
            .Select(i => new InvoiceLineSnap
            {
                LineNo = i,
                Description = $"Item {i}",
                Qty = 1,
                Amount = 100,
                TaxableAmount = 95,
            })
            .ToList();

        var chunks = InvoiceLinePagination.ChunkLinesFillThenFooterPage(lines, pageMax: 22, lastPageMax: 15);

        Assert.Equal(2, chunks.Count);
        Assert.Equal(18, chunks[0].Count);
        Assert.Empty(chunks[1]);
    }

    [Fact]
    public void ChunkLinesFillThenFooterPage_fills_full_pages_before_last()
    {
        var lines = Enumerable.Range(1, 28)
            .Select(i => new InvoiceLineSnap
            {
                LineNo = i,
                Description = $"Item {i}",
                Qty = 1,
                Amount = 100,
                TaxableAmount = 95,
            })
            .ToList();

        var chunks = InvoiceLinePagination.ChunkLinesFillThenFooterPage(lines, pageMax: 22, lastPageMax: 15);

        Assert.Equal(2, chunks.Count);
        Assert.Equal(22, chunks[0].Count);
        Assert.Equal(6, chunks[1].Count);
    }

    private static T OnSta<T>(Func<T> action)
    {
        T? result = default;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null)
            throw new Exception(error.ToString());
        return result!;
    }

    private static ThermalInvoiceInput SampleBill(bool isInterState, StoreProfile? store = null, int lineCount = 1)
    {
        var lines = new List<InvoiceLineSnap>();
        var taxablePerLine = 1000m;
        for (var i = 1; i <= lineCount; i++)
        {
            lines.Add(new InvoiceLineSnap
            {
                LineNo = i,
                Description = $"SUIT ITEM {i}-PARTY WEAR-SKU-{i:D5}",
                Hsn = "540710",
                Qty = 2m,
                Rate = 525m,
                TaxPercent = 5m,
                TaxableAmount = taxablePerLine,
                TaxAmount = 50m,
                LineInclusiveAmount = 1050m,
                Amount = 1050m,
            });
        }

        var taxable = taxablePerLine * lineCount;
        var tax = MoneyMath.RoundAmount(taxable * 0.05m);
        return new ThermalInvoiceInput
        {
            Store = store ?? new StoreProfile
            {
                StoreName = "RR Bridal Test",
                Address = "Hyderabad",
                Gstin = "36ABFFR4340C1ZI",
                StateName = "Telangana",
            },
            CharWidth = 48,
            BillNo = "198",
            BillDate = "20-AUG-26",
            UserName = "Cashier",
            Time = "20:30",
            Counter = "01",
            CustomerName = "Sample Customer",
            CustomerPhone = "9876543210",
            IsInterState = isInterState,
            CgstTotal = isInterState ? 0m : tax / 2m,
            SgstTotal = isInterState ? 0m : tax / 2m,
            IgstTotal = isInterState ? tax : 0m,
            TotalTaxableAmount = taxable,
            TotalQty = 2m * lineCount,
            Payable = taxable + tax,
            SubTotal = taxable,
            Lines = lines,
        };
    }

    private static int CountDocumentPages(FlowDocument document)
    {
        var pages = 0;
        foreach (var block in document.Blocks)
        {
            if (block is BlockUIContainer)
                pages++;
            else if (block is Section section)
            {
                foreach (var _ in section.Blocks.OfType<BlockUIContainer>())
                    pages++;
            }
        }

        return pages;
    }

    private static string CollectDocumentText(FlowDocument document)
    {
        var parts = new List<string>();
        foreach (var block in document.Blocks)
            CollectBlockText(block, parts);
        return string.Join('\n', parts);
    }

    private static void CollectBlockText(Block block, List<string> parts)
    {
        switch (block)
        {
            case BlockUIContainer ui when ui.Child != null:
                CollectLogicalText(ui.Child, parts);
                break;
            case Section section:
                foreach (var child in section.Blocks)
                    CollectBlockText(child, parts);
                break;
            case Paragraph paragraph:
                parts.Add(new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text);
                break;
        }
    }

    private static void CollectLogicalText(DependencyObject? node, List<string> parts)
    {
        if (node == null)
            return;

        if (node is TextBlock tb && !string.IsNullOrWhiteSpace(tb.Text))
            parts.Add(tb.Text);

        if (node is Panel panel)
        {
            foreach (UIElement child in panel.Children)
                CollectLogicalText(child, parts);
            return;
        }

        if (node is Decorator decorator)
        {
            CollectLogicalText(decorator.Child, parts);
            return;
        }

        if (node is ContentControl content && content.Content is DependencyObject dep)
            CollectLogicalText(dep, parts);
    }
}
