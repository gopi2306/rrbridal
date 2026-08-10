using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace RRBridal.StoreBilling.UiTests.Tests.P1P2;

[Trait("Category", "FixtureContract")]
[Trait("Area", "P1P2Routes")]
public sealed class P1P2FixtureContractTests
{
    [Fact]
    [Trait("Priority", "P1")]
    public async Task DailyExpenses_CreateUpdateAndListPreserveState()
    {
        using var server = FakeCentralServer.Start();
        using var client = Client(server);

        await Success(client.PostAsJsonAsync("/api/store-pos/daily-expenses", new
        {
            payload = new
            {
                expenseNo = "EXP-P1-001",
                supplierName = "Contract Supplier",
                category = "Utilities",
                amount = 125.50m,
                status = "posted",
            },
        }));
        await Success(client.PutAsJsonAsync("/api/store-pos/daily-expenses/EXP-P1-001", new
        {
            payload = new
            {
                expenseNo = "EXP-P1-001",
                supplierName = "Contract Supplier",
                category = "Utilities",
                amount = 150m,
                status = "posted",
            },
        }));

        var expenses = await client.GetFromJsonAsync<JsonElement>(
            "/api/store-pos/daily-expenses?storeCode=ui-test-store&businessDate=2026-08-07");

        Assert.Equal(1, expenses.GetArrayLength());
        Assert.Equal(150m, expenses[0].GetProperty("amount").GetDecimal());
        Assert.Equal("Contract Supplier", expenses[0].GetProperty("supplierName").GetString());
        Assert.Contains(server.State.Requests, request => request.Method == "PUT");
    }

    [Fact]
    [Trait("Priority", "P1")]
    public async Task Bills_HeldBillsAndReturnDocumentsSupportStatefulLifecycle()
    {
        using var server = FakeCentralServer.Start();
        using var client = Client(server);

        await Success(client.PostAsJsonAsync("/api/store-pos/bills", new
        {
            payload = new
            {
                billNo = "BILL-P1-001",
                customerName = "UI Test Customer",
                payable = 4400m,
                status = "posted",
            },
        }));
        var bill = await client.GetFromJsonAsync<JsonElement>("/api/store-pos/bills/BILL-P1-001");
        Assert.Equal("UI Test Customer", bill.GetProperty("customerName").GetString());

        await Success(client.PutAsJsonAsync("/api/store-pos/held-bills/HOLD-P1-001", new
        {
            payload = new { holdNo = "HOLD-P1-001", customerName = "Held Customer", payable = 1200m },
        }));
        Assert.Single((await client.GetFromJsonAsync<JsonElement>("/api/store-pos/held-bills")).EnumerateArray());
        await Success(client.DeleteAsync("/api/store-pos/held-bills/HOLD-P1-001"));
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/store-pos/held-bills")).EnumerateArray());

        await Success(client.PostAsJsonAsync("/api/store-pos/sale-returns", new
        {
            payload = new
            {
                returnNo = "RET-P1-001",
                originalBillNo = "BILL-P1-001",
                returnTotal = 500m,
                status = "posted",
            },
        }));
        var saleReturn = await client.GetFromJsonAsync<JsonElement>("/api/store-pos/sale-returns/RET-P1-001");
        Assert.Equal("BILL-P1-001", saleReturn.GetProperty("originalBillNo").GetString());

        await Success(client.DeleteAsync("/api/store-pos/bills/BILL-P1-001"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/store-pos/bills/BILL-P1-001")).StatusCode);
    }

    [Fact]
    [Trait("Priority", "P1")]
    public async Task QuotationsCreditNotesPaymentsAndAdjustmentsAreQueryableAfterWrites()
    {
        using var server = FakeCentralServer.Start();
        using var client = Client(server);

        await Success(client.PostAsJsonAsync("/api/store-pos/quotations", new
        {
            payload = new { quotationNo = "QUOT-P1-001", customerName = "Quote Customer", payable = 900m },
        }));
        await Success(client.PostAsJsonAsync("/api/store-pos/credit-notes", new
        {
            payload = new { creditNoteNo = "CN-P1-001", customerName = "Credit Customer", balance = 250m },
        }));
        await Success(client.PostAsJsonAsync("/api/store-pos/gateway-payments", new
        {
            payload = new { receiptNo = "RCPT-P1-001", invoiceNo = "BILL-P1-001", amount = 250m },
        }));
        await Success(client.PostAsJsonAsync("/api/store-pos/adjustment-bills", new
        {
            payload = new { adjustmentNo = "ADJ-P1-001", originalBillNo = "BILL-P1-001", amount = 50m },
        }));

        Assert.Equal(
            "Quote Customer",
            (await client.GetFromJsonAsync<JsonElement>("/api/store-pos/quotations/QUOT-P1-001"))
                .GetProperty("customerName").GetString());
        Assert.Equal(
            250m,
            (await client.GetFromJsonAsync<JsonElement>("/api/store-pos/credit-notes/CN-P1-001"))
                .GetProperty("balance").GetDecimal());
        Assert.Equal(
            "BILL-P1-001",
            (await client.GetFromJsonAsync<JsonElement>("/api/store-pos/payment-receipts/RCPT-P1-001"))
                .GetProperty("invoiceNo").GetString());
        Assert.Single((await client.GetFromJsonAsync<JsonElement>("/api/store-pos/adjustment-bills")).EnumerateArray());
    }

    [Fact]
    [Trait("Priority", "P2")]
    public async Task DashboardInventoryMastersAndScreenAccessReturnSeededFunctionalData()
    {
        using var server = FakeCentralServer.Start();
        using var client = Client(server);

        var mode = await client.GetFromJsonAsync<JsonElement>("/api/sync/store-pos-mode");
        var access = mode.GetProperty("posBillingSettings").GetProperty("screenAccess");
        Assert.Contains("1", access.GetProperty("billing").EnumerateArray().Select(value => value.GetString()));
        Assert.Contains("1", access.GetProperty("dashboard").EnumerateArray().Select(value => value.GetString()));

        var inventory = await client.GetFromJsonAsync<JsonElement>("/api/inventory/grid?search=SAREE");
        Assert.Equal(2, inventory.GetProperty("total").GetInt32());
        Assert.Equal("UI-SAREE-001", inventory.GetProperty("items")[0].GetProperty("sku").GetString());

        var customers = await client.GetFromJsonAsync<JsonElement>("/api/customers");
        var salesmen = await client.GetFromJsonAsync<JsonElement>("/api/salesmen");
        var dashboard = await client.GetFromJsonAsync<JsonElement>("/api/dashboard/store-sales");
        Assert.Equal("UI Test Customer", customers[0].GetProperty("name").GetString());
        Assert.Equal("UI Test Salesman", salesmen[0].GetProperty("name").GetString());
        Assert.Equal(1000m, dashboard.GetProperty("summary").GetProperty("expectedCash").GetDecimal());
    }

    [Fact]
    [Trait("Priority", "P2")]
    public async Task EmptyReportingAndLookupRoutesHaveTruthfulStableShapes()
    {
        using var server = FakeCentralServer.Start();
        using var client = Client(server);

        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/store-pos/bills")).EnumerateArray());
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/store-pos/quotations")).EnumerateArray());
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/store-pos/sale-returns")).EnumerateArray());
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/store-pos/credit-notes")).EnumerateArray());
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/store-pos/gateway-payments")).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/store-pos/bills/UNKNOWN")).StatusCode);
    }

    [Fact]
    [Trait("Priority", "P2")]
    public async Task NumberAllocationAndDaySessionStateAreDeterministic()
    {
        using var server = FakeCentralServer.Start(new FakeCentralScenario { SeedOpenDaySession = false });
        using var client = Client(server);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/store-pos/day-sessions/current")).StatusCode);

        var first = await PostForJson(client, "/api/store-pos/next-number", new { kind = "quotation" });
        var second = await PostForJson(client, "/api/store-pos/next-number", new { kind = "quotation" });
        Assert.Equal("QUOT-UI-0001", first.GetProperty("value").GetString());
        Assert.Equal("QUOT-UI-0002", second.GetProperty("value").GetString());

        await Success(client.PostAsJsonAsync("/api/store-pos/day-sessions/open", new
        {
            payload = new
            {
                sessionId = "session-p2",
                storeId = FakeCentralState.StoreId,
                businessDate = "2026-08-07",
                status = "open",
                openingCash = 1000m,
            },
        }));
        var current = await client.GetFromJsonAsync<JsonElement>("/api/store-pos/day-sessions/current");
        Assert.Equal("open", current.GetProperty("payload").GetProperty("status").GetString());
    }

    private static HttpClient Client(FakeCentralServer server) => new() { BaseAddress = server.BaseAddress };

    private static async Task Success(Task<HttpResponseMessage> responseTask)
    {
        using var response = await responseTask;
        response.EnsureSuccessStatusCode();
    }

    private static async Task<JsonElement> PostForJson(HttpClient client, string path, object payload)
    {
        using var response = await client.PostAsJsonAsync(path, payload);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
