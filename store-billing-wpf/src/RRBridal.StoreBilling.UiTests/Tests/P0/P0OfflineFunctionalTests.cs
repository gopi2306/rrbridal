using FlaUI.Core.AutomationElements;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.UiTests.Pages;
using RRBridal.StoreBilling.UiTests.Workflows;
using Xunit;

namespace RRBridal.StoreBilling.UiTests.Tests.P0;

[Collection(UiTestCollection.Name)]
[Trait("Category", "UiOffline")]
[Trait("Priority", "P0")]
public sealed class P0OfflineFunctionalTests : UiTestBase
{
    public P0OfflineFunctionalTests(BillingAppFixture fixture) : base(fixture) { }

    [SkippableFact]
    [Trait("Area", "Authentication")]
    public void ValidOfflineLogin_ShowsMongoAndSeededOpenDayStatus()
    {
        UiTestEnvironment.SkipUnlessRunnable();

        RunWithDiagnostics(
            nameof(ValidOfflineLogin_ShowsMongoAndSeededOpenDayStatus),
            UiTestLaunchOptions.Offline,
            session =>
            {
                P0UiTestSteps.SignIn(session);

                P0UiTestSteps.WaitForVisibleText(session, "Central: Offline");
                P0UiTestSteps.WaitForVisibleText(session, "Mongo: Connected");
                P0UiTestSteps.WaitForVisibleText(session, "Day: Open");
                Assert.Equal(
                    1,
                    session.MongoDatabase
                        .GetCollection<BsonDocument>("store_day_sessions")
                        .CountDocuments(Builders<BsonDocument>.Filter.Eq("status", "open")));
            });
    }

    [SkippableFact]
    [Trait("Category", "UiTransactional")]
    [Trait("Area", "Billing")]
    public void CashPost_PersistsStoreBillAndPendingOutbox_VisibleInNotifications()
    {
        UiTestEnvironment.SkipUnlessRunnable();

        RunWithDiagnostics(
            nameof(CashPost_PersistsStoreBillAndPendingOutbox_VisibleInNotifications),
            UiTestLaunchOptions.Offline,
            session =>
            {
                P0UiTestSteps.SignIn(session);
                var billing = P0UiTestSteps.PrepareCashBill(session);
                billing.SetPrintInvoice(false);

                new BillingWorkflow(session).PostCashBill(
                    _ => P0UiTestSteps.InvokeVisibleButtonById(session, "main-post-bill-button"),
                    "5000");

                var bills = session.MongoDatabase.GetCollection<BsonDocument>("store_bills");
                var outbox = session.MongoDatabase.GetCollection<BsonDocument>("outbox_events");
                Wait.Until(
                    () => bills.CountDocuments(FilterDefinition<BsonDocument>.Empty) == 1
                          && outbox.CountDocuments(Builders<BsonDocument>.Filter.And(
                              Builders<BsonDocument>.Filter.Eq("type", "InvoiceCreated"),
                              Builders<BsonDocument>.Filter.Eq("status", "pending"))) == 1,
                    UiTestEnvironment.ActionTimeout,
                    "one local bill and one pending InvoiceCreated outbox event");

                var bill = bills.Find(FilterDefinition<BsonDocument>.Empty).Single();
                var billNo = bill.GetValue("billNo").AsString;
                Assert.Contains(
                    "UI Test Customer",
                    bill.GetValue("customerName").AsString,
                    StringComparison.Ordinal);
                Assert.Contains(
                    bill.GetValue("lines").AsBsonArray,
                    line => line.AsBsonDocument.GetValue("sku", "").AsString == "UI-SAREE-001");
                Assert.DoesNotContain(
                    session.CentralState.Requests,
                    request => request.Method == "POST"
                               && request.PathAndQuery == "/api/store-pos/bills");

                P0UiTestSteps.InvokeVisibleButtonById(session, "main-notifications-button");
                var notifications = new NotificationsPage(session);
                P0UiTestSteps.WaitForVisibleText(session, "InvoiceCreated");
                P0UiTestSteps.WaitForVisibleText(session, billNo);
                notifications.Close();
            });
    }

    [SkippableFact]
    [Trait("Category", "UiTransactional")]
    [Trait("Area", "DayClose")]
    public void PostingWithoutOpenDay_ShowsGuardAndDoesNotWrite()
    {
        UiTestEnvironment.SkipUnlessRunnable();
        var options = UiTestLaunchOptions.Offline with
        {
            SeedOpenDaySession = false,
            CentralScenario = new FakeCentralScenario { SeedOpenDaySession = false },
        };

        RunWithDiagnostics(
            nameof(PostingWithoutOpenDay_ShowsGuardAndDoesNotWrite),
            options,
            session =>
            {
                P0UiTestSteps.SignIn(session);
                P0UiTestSteps.WaitForVisibleText(session, "Day: Not opened");
                var billing = P0UiTestSteps.PrepareCashBill(session);

                billing.Post();
                Thread.Sleep(250);
                if (!session.Application.GetAllTopLevelWindows(session.Automation).Any(window =>
                        string.Equals(window.AutomationId, "app-message-dialog", StringComparison.Ordinal)
                        || window.FindFirstDescendant(cf => cf.ByAutomationId("app-message-dialog")) is not null))
                {
                    P0UiTestSteps.InvokeVisibleButtonById(session, "main-post-bill-button");
                }
                var guard = new AppMessagePage(session);
                Assert.Contains("Open the day before posting", guard.Body, StringComparison.OrdinalIgnoreCase);
                guard.Close();

                Assert.Equal(
                    0,
                    session.MongoDatabase
                        .GetCollection<BsonDocument>("store_bills")
                        .CountDocuments(FilterDefinition<BsonDocument>.Empty));
                Assert.Equal(
                    0,
                    session.MongoDatabase
                        .GetCollection<BsonDocument>("outbox_events")
                        .CountDocuments(Builders<BsonDocument>.Filter.Eq("type", "InvoiceCreated")));
            });
    }
}
