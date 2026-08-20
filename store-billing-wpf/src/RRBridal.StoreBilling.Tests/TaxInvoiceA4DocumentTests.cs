using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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

    private static ThermalInvoiceInput SampleBill(bool isInterState, StoreProfile? store = null) =>
        new()
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
            CgstTotal = isInterState ? 0m : 25m,
            SgstTotal = isInterState ? 0m : 25m,
            IgstTotal = isInterState ? 50m : 0m,
            TotalTaxableAmount = 1000m,
            TotalQty = 2m,
            Payable = 1050m,
            SubTotal = 1000m,
            Lines =
            [
                new InvoiceLineSnap
                {
                    LineNo = 1,
                    Description = "SUIT",
                    Hsn = "540710",
                    Qty = 2m,
                    Rate = 525m,
                    TaxPercent = 5m,
                    TaxableAmount = 1000m,
                    TaxAmount = 50m,
                    LineInclusiveAmount = 1050m,
                    Amount = 1050m,
                },
            ],
        };

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
