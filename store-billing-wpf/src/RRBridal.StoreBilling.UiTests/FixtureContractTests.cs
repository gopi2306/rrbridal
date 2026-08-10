using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace RRBridal.StoreBilling.UiTests;

[Trait("Category", "FixtureContract")]
public sealed class FixtureContractTests
{
    [Fact]
    public void MongoGuard_AcceptsOnlyGeneratedUiDatabaseNames()
    {
        LocalMongoFixture.EnsureSafeDatabaseName(
            LocalMongoFixture.DatabasePrefix + Guid.NewGuid().ToString("N"));

        Assert.Throws<InvalidOperationException>(
            () => LocalMongoFixture.EnsureSafeDatabaseName("rr_bridal_store"));
        Assert.Throws<InvalidOperationException>(
            () => LocalMongoFixture.EnsureSafeDatabaseName("rr_bridal_ui_"));
        Assert.Throws<InvalidOperationException>(
            () => LocalMongoFixture.EnsureSafeDatabaseName("rr_bridal_ui_test"));
    }

    [Fact]
    public void FakeState_IsStatefulAndSearchesCatalog()
    {
        var state = new FakeCentralState(new());
        using var bill = JsonDocument.Parse("""{"billNo":"BILL-UI-42","grandTotal":1234}""");

        state.StoreBill(bill.RootElement);

        Assert.NotNull(state.GetBill("BILL-UI-42"));
        Assert.Single(state.Bills);
        Assert.Contains(state.SearchCatalog("SAREE"), item => item is not null);
        Assert.StartsWith("QUOT-UI-", state.NextNumber("quotation"));
    }

    [Fact]
    public async Task FakeServer_RecordsRequestsAndPersistsHeldBills()
    {
        using var server = FakeCentralServer.Start();
        using var client = new HttpClient { BaseAddress = server.BaseAddress };

        using var save = await client.PutAsJsonAsync(
            "/api/store-pos/held-bills/HOLD-UI-1",
            new
            {
                storeId = FakeCentralState.StoreId,
                deviceId = "ui-test-device",
                payload = new { holdNo = "HOLD-UI-1", amount = 500 },
            });
        save.EnsureSuccessStatusCode();

        var held = await client.GetFromJsonAsync<JsonElement>(
            $"/api/store-pos/held-bills?storeCode={FakeCentralState.StoreId}");

        Assert.Equal(JsonValueKind.Array, held.ValueKind);
        Assert.Equal(1, held.GetArrayLength());
        Assert.Contains(
            server.State.Requests,
            request => request.Method == "PUT"
                       && request.PathAndQuery.StartsWith("/api/store-pos/held-bills/HOLD-UI-1"));
    }

    [Fact]
    public async Task FakeServer_SyncPushAcknowledgesEveryEvent()
    {
        using var server = FakeCentralServer.Start();
        using var client = new HttpClient { BaseAddress = server.BaseAddress };
        using var response = await client.PostAsJsonAsync(
            "/api/sync/push",
            new
            {
                events = new[]
                {
                    new { eventId = "event-1", type = "BillPosted", payload = new { } },
                    new { eventId = "event-2", type = "ExpenseCreated", payload = new { } },
                },
            });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var results = payload.GetProperty("results");
        Assert.Equal(2, results.GetArrayLength());
        Assert.All(results.EnumerateArray(), result => Assert.Equal("applied", result.GetProperty("status").GetString()));
    }
}
