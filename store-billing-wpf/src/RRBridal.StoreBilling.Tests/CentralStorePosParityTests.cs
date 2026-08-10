using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Api;
using RRBridal.StoreBilling.App.Services.PurchaseIntents;
using RRBridal.StoreBilling.App.Services.Sync;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public sealed class CentralStorePosParityTests
{
    [Fact]
    public async Task ListAwaitingTransfersAsync_maps_transfer_payload()
    {
        var handler = new RecordingHandler(_ => JsonResponse(
            """
            [{
              "transferId":"507f1f77bcf86cd799439011",
              "transferNo":"TR-1001",
              "direction":"warehouse_to_store",
              "status":"awaiting_intake",
              "lines":[{"sku":"SKU-1","qty":2}]
            }]
            """));
        var client = CreateClient(handler);

        var transfers = await client.ListAwaitingTransfersAsync();

        var transfer = Assert.Single(transfers);
        Assert.Equal("TR-1001", transfer.TransferNo);
        Assert.Equal("warehouse_to_store", transfer.Direction);
        Assert.Equal(2m, Assert.Single(transfer.Lines).Qty);
        Assert.Contains("/api/store-pos/transfers/awaiting-intake", handler.LastRequestUri);
    }

    [Fact]
    public async Task PostEventAsync_sends_deterministic_event_id()
    {
        var handler = new RecordingHandler(_ => JsonResponse("""{"status":"applied"}"""));
        var context = new StoreContext();
        var client = new CentralStorePosClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://central.test") },
            context);

        await client.PostEventAsync(
            "StockTransferReceived",
            new { transferId = "transfer-1", lines = new[] { new { sku = "SKU-1", qty = 2 } } },
            eventId: "stock-transfer-received:store-001:transfer-1");

        using var body = JsonDocument.Parse(handler.LastBody);
        Assert.Equal(
            "stock-transfer-received:store-001:transfer-1",
            body.RootElement.GetProperty("eventId").GetString());
        Assert.Equal("StockTransferReceived", body.RootElement.GetProperty("type").GetString());
        Assert.Equal(context.StoreId, body.RootElement.GetProperty("storeId").GetString());
    }

    [Fact]
    public async Task PurchaseIntentPublisher_posts_directly_without_connecting_to_mongo()
    {
        var context = new StoreContext();
        var db = new MongoClient(
                "mongodb://127.0.0.1:27099/?serverSelectionTimeoutMS=10&connectTimeoutMS=10")
            .GetDatabase("parity_test");
        var outbox = new BillingOutboxPublisher(db, context);
        string? postedType = null;
        string? postedEventId = null;
        outbox.ConfigureOnlineDispatch(
            () => true,
            _ => Task.CompletedTask,
            (type, _, _, eventId, _) =>
            {
                postedType = type;
                postedEventId = eventId;
                return Task.CompletedTask;
            });
        var publisher = new PurchaseIntentPublisher(db, context, outbox);

        var eventId = await publisher.SubmitAsync(
            [new PurchaseIntentLineInput("SKU-1", 2)],
            "replenish",
            CancellationToken.None);

        Assert.Equal("PurchaseIntentCreated", postedType);
        Assert.Equal(eventId, postedEventId);
    }

    private static CentralStorePosClient CreateClient(RecordingHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("http://central.test") },
            new StoreContext());

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public string LastRequestUri { get; private set; } = "";
        public string LastBody { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri?.ToString() ?? "";
            LastBody = request.Content == null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }
}
