using RRBridal.StoreBilling.App.Services.Invoicing;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public sealed class InvoiceLineSnapTests
{
    [Fact]
    public void PrePrintedLineAmount_UsesInclusiveWhenSet()
    {
        var line = new InvoiceLineSnap
        {
            Amount = 5399m,
            TaxableAmount = 3856.43m,
            TaxAmount = 192.57m,
            LineInclusiveAmount = 4049m,
        };

        Assert.Equal(4049m, line.PrePrintedLineAmount());
    }

    [Fact]
    public void PrePrintedLineAmount_FallsBackToTaxablePlusTax()
    {
        var line = new InvoiceLineSnap
        {
            Amount = 5399m,
            TaxableAmount = 3856.43m,
            TaxAmount = 192.57m,
        };

        Assert.Equal(4049m, line.PrePrintedLineAmount());
    }

    [Fact]
    public void PrePrintedLineAmount_FallsBackToGrossAmount()
    {
        var line = new InvoiceLineSnap
        {
            Amount = 1500m,
        };

        Assert.Equal(1500m, line.PrePrintedLineAmount());
    }
}
