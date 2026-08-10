using FlaUI.Core.WindowsAPI;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace RRBridal.StoreBilling.UiTests;

[Collection(UiTestCollection.Name)]
[Trait("Category", "UiSmoke")]
[Trait("Category", "UiOffline")]
public sealed class OfflineSmokeTests : UiTestBase
{
    public OfflineSmokeTests(BillingAppFixture fixture)
        : base(fixture)
    {
    }

    [SkippableFact]
    public void OfflineLogin_UsesIsolatedMongoAndOpensShell()
    {
        UiTestEnvironment.SkipUnlessRunnable();

        RunWithDiagnostics(
            nameof(OfflineLogin_UsesIsolatedMongoAndOpensShell),
            UiTestLaunchOptions.Offline,
            session =>
            {
                Assert.StartsWith(LocalMongoFixture.DatabasePrefix, session.MongoDatabaseName);
                Assert.Equal(
                    1,
                    session.MongoDatabase
                        .GetCollection<BsonDocument>("store_users")
                        .CountDocuments(FilterDefinition<BsonDocument>.Empty));

                session.Login.SignIn(FakeCentralServer.ValidEmail, FakeCentralServer.ValidPassword);
                var shell = session.WaitForShell();

                KeyboardHelper.PressControlShortcut(VirtualKeyShort.KEY_A);
                Assert.NotNull(shell.WaitForPage("billing-view", "Billing"));
            });
    }

    [SkippableFact]
    public void OfflineInvalidLogin_RemainsOnLoginScreen()
    {
        UiTestEnvironment.SkipUnlessRunnable();

        RunWithDiagnostics(
            nameof(OfflineInvalidLogin_RemainsOnLoginScreen),
            UiTestLaunchOptions.Offline,
            session =>
            {
                session.Login.SignIn(FakeCentralServer.ValidEmail, "wrong-password");

                Assert.NotNull(session.Login.WaitForError("Invalid email or password."));
                Assert.True(session.Login.IsDisplayed());
            });
    }
}
