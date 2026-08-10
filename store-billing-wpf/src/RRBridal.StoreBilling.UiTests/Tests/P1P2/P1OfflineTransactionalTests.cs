using FlaUI.Core.WindowsAPI;
using RRBridal.StoreBilling.UiTests.Pages;
using Xunit;

namespace RRBridal.StoreBilling.UiTests.Tests.P1P2;

[Collection(UiTestCollection.Name)]
[Trait("Category", "UiOffline")]
[Trait("Category", "UiTransactional")]
[Trait("Priority", "P1")]
[Trait("Area", "OfflineExpenses")]
public sealed class P1OfflineTransactionalTests : UiTestBase
{
    public P1OfflineTransactionalTests(BillingAppFixture fixture) : base(fixture) { }

    [SkippableFact]
    public void DailyExpense_InvalidDraftDoesNotPersistOffline()
    {
        UiTestEnvironment.SkipUnlessRunnable();

        RunWithDiagnostics(
            nameof(DailyExpense_InvalidDraftDoesNotPersistOffline),
            UiTestLaunchOptions.Offline,
            session =>
            {
                P1OnlineFunctionalTests.SignIn(session);
                P1OnlineFunctionalTests.Navigate(session, VirtualKeyShort.KEY_E, "daily-expense-page", "Daily Expenses");
                var page = new DailyExpensesPage(session);
                page.Open();
                page.Save();

                var validation = new AppMessagePage(session);
                Assert.Equal("Daily Expense", validation.Title);
                Assert.Contains("valid amount", validation.Body, StringComparison.OrdinalIgnoreCase);
                validation.Close();
                Assert.Equal(
                    0,
                    session.MongoDatabase
                        .GetCollection<MongoDB.Bson.BsonDocument>("store_daily_expenses")
                        .CountDocuments(MongoDB.Driver.FilterDefinition<MongoDB.Bson.BsonDocument>.Empty));
                Assert.DoesNotContain(
                    session.CentralState.Requests,
                    request => request.Method == "POST"
                               && request.PathAndQuery.StartsWith("/api/store-pos/daily-expenses", StringComparison.Ordinal));
            });
    }

    [SkippableFact]
    [Trait("Area", "OfflineMasters")]
    public void SeededCustomerAndSalesmanRemainUsableOffline()
    {
        UiTestEnvironment.SkipUnlessRunnable();

        RunWithDiagnostics(
            nameof(SeededCustomerAndSalesmanRemainUsableOffline),
            UiTestLaunchOptions.Offline,
            session =>
            {
                P1OnlineFunctionalTests.SignIn(session);

                P1OnlineFunctionalTests.Navigate(session, VirtualKeyShort.KEY_U, "customers-page", "Customers");
                var customers = new CustomersPage(session);
                customers.Open();
                customers.Search();
                P1OnlineFunctionalTests.AssertVisibleText(session, "UI Test Customer");

                P1OnlineFunctionalTests.Navigate(session, VirtualKeyShort.KEY_M, "salesmen-page", "Salesman");
                var salesmen = new SalesmenPage(session);
                salesmen.Open();
                salesmen.Search();
                P1OnlineFunctionalTests.AssertVisibleText(session, "UI Test Salesman");
            });
    }
}
