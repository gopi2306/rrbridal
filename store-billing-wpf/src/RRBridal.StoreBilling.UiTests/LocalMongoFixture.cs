using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace RRBridal.StoreBilling.UiTests;

internal sealed class LocalMongoFixture : IDisposable
{
    internal const string DatabasePrefix = "rr_bridal_ui_";
    private static readonly Regex SafeDatabaseName = new(
        "^rr_bridal_ui_[a-f0-9]{32}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IMongoClient _client;
    private readonly bool _ownsDatabase;
    private bool _disposed;

    private LocalMongoFixture(
        IMongoClient client,
        string databaseName,
        string connectionUri,
        bool ownsDatabase)
    {
        _client = client;
        DatabaseName = databaseName;
        ConnectionUri = connectionUri;
        _ownsDatabase = ownsDatabase;
    }

    public string DatabaseName { get; }

    public string ConnectionUri { get; }

    internal IMongoDatabase Database => _client.GetDatabase(DatabaseName);

    public static async Task<LocalMongoFixture> CreateAsync(
        UiTestLaunchOptions options,
        CancellationToken cancellationToken = default)
    {
        var configured = options.MongoUri
            ?? Environment.GetEnvironmentVariable("RRBRIDAL_UI_MONGO_URI")
            ?? "mongodb://127.0.0.1:27017";
        var sourceUrl = new MongoUrl(configured);
        var databaseName = DatabasePrefix + Guid.NewGuid().ToString("N");
        EnsureSafeDatabaseName(databaseName);

        var builder = new MongoUrlBuilder(sourceUrl.ToString())
        {
            DatabaseName = databaseName,
        };
        var settings = MongoClientSettings.FromUrl(builder.ToMongoUrl());
        settings.ConnectTimeout = options.Mode == PosMode.Online
            ? TimeSpan.FromMilliseconds(250)
            : TimeSpan.FromSeconds(5);
        settings.ServerSelectionTimeout = options.Mode == PosMode.Online
            ? TimeSpan.FromMilliseconds(250)
            : TimeSpan.FromSeconds(5);
        var client = new MongoClient(settings);

        if (options.Mode == PosMode.Online)
            return new LocalMongoFixture(client, databaseName, builder.ToString(), ownsDatabase: true);

        try
        {
            await client.GetDatabase("admin")
                .RunCommandAsync<BsonDocument>(
                    new BsonDocument("ping", 1),
                    cancellationToken: cancellationToken);

            var fixture = new LocalMongoFixture(client, databaseName, builder.ToString(), ownsDatabase: true);
            await fixture.SeedAsync(options, cancellationToken);
            return fixture;
        }
        catch
        {
            await TryDropAsync(client, databaseName, cancellationToken);
            throw;
        }
    }

    internal static void EnsureSafeDatabaseName(string databaseName)
    {
        if (!SafeDatabaseName.IsMatch(databaseName))
        {
            throw new InvalidOperationException(
                $"Refusing Mongo operation for unsafe UI-test database name '{databaseName}'.");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (!_ownsDatabase)
            return;

        EnsureSafeDatabaseName(DatabaseName);
        try
        {
            _client.DropDatabase(DatabaseName);
        }
        catch
        {
            // Cleanup must not obscure the UI-test result. The unique prefix keeps leftovers isolated.
        }
    }

    private async Task SeedAsync(UiTestLaunchOptions options, CancellationToken cancellationToken)
    {
        EnsureSafeDatabaseName(DatabaseName);
        var db = _client.GetDatabase(DatabaseName);
        var now = DateTime.UtcNow;

        await db.GetCollection<BsonDocument>("store_users").InsertOneAsync(
            new BsonDocument
            {
                { "centralId", FakeCentralState.UserId },
                { "email", FakeCentralServer.ValidEmail },
                { "name", "UI Test User" },
                { "role", "admin" },
                { "passwordHash", BCrypt.Net.BCrypt.HashPassword(FakeCentralServer.ValidPassword) },
                { "storeId", FakeCentralState.StoreId },
                { "locationKind", "store" },
                { "maxDiscountPercent", 100d },
                { "lastSyncedAt", now.ToString("O") },
            },
            cancellationToken: cancellationToken);

        await db.GetCollection<BsonDocument>("local_products_cache").InsertManyAsync(
            FakeCentralState.CatalogProducts.Select(product => new BsonDocument
            {
                { "centralProductId", product.CentralId },
                { "sku", product.Sku },
                { "upcEanCode", product.UpcEanCode },
                { "itemName", product.Name },
                { "shortName", product.ShortName },
                { "alias", product.Alias },
                { "costPrice", (double)product.CostPrice },
                { "mrp", (double)product.Mrp },
                { "sellingPrice", (double)product.SellingPrice },
                { "storePrice", (double)product.StorePrice },
                { "gstPercent", (double)product.GstPercent },
                { "hsnSac", product.HsnSac },
                { "stockQty", (double)product.StockQty },
                { "lastSyncedAt", now.ToString("O") },
            }),
            cancellationToken: cancellationToken);

        await db.GetCollection<BsonDocument>("store_customers").InsertOneAsync(
            new BsonDocument
            {
                { "storeId", FakeCentralState.StoreId },
                { "centralCustomerId", FakeCentralState.CustomerId },
                { "customerCode", "CUST-UI-001" },
                { "name", "UI Test Customer" },
                { "phone", "9000000001" },
                { "mobile", "9000000001" },
                { "email", "customer@rrbridal.test" },
                { "fullAddress", "UI Test Address" },
                { "city", "Bengaluru" },
                { "state", "Karnataka" },
                { "pincode", "560001" },
                { "isCreditCustomer", true },
                { "centralSyncStatus", "synced" },
                { "createdAtUtc", now.ToString("O") },
            },
            cancellationToken: cancellationToken);

        await db.GetCollection<BsonDocument>("store_salesmen").InsertOneAsync(
            new BsonDocument
            {
                { "storeId", FakeCentralState.StoreId },
                { "centralId", FakeCentralState.SalesmanId },
                { "salesmanCode", "SM-UI-001" },
                { "name", "UI Test Salesman" },
                { "phone", "9000000002" },
                { "isActive", true },
                { "centralSyncStatus", "synced" },
                { "createdAtUtc", now.ToString("O") },
            },
            cancellationToken: cancellationToken);

        if (options.SeedOpenDaySession)
        {
            await db.GetCollection<BsonDocument>("store_day_sessions").InsertOneAsync(
                FakeCentralState.CreateOpenDaySessionDocument(),
                cancellationToken: cancellationToken);
        }
    }

    private static async Task TryDropAsync(
        IMongoClient client,
        string databaseName,
        CancellationToken cancellationToken)
    {
        EnsureSafeDatabaseName(databaseName);
        try
        {
            await client.DropDatabaseAsync(databaseName, cancellationToken);
        }
        catch
        {
            // The original setup failure is more useful than cleanup failure.
        }
    }
}
