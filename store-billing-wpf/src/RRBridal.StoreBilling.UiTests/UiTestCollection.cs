using Xunit;

namespace RRBridal.StoreBilling.UiTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class UiTestCollection : ICollectionFixture<BillingAppFixture>
{
    public const string Name = "WPF UI automation";
}
