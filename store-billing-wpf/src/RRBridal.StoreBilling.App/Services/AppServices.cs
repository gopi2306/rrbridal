using System;
using System.Net.Http;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services.Auth;
using RRBridal.StoreBilling.App.Services.Inventory;
using RRBridal.StoreBilling.App.Services.Payments;
using RRBridal.StoreBilling.App.Services.Products;
using RRBridal.StoreBilling.App.Services.PurchaseIntents;
using RRBridal.StoreBilling.App.Services.Invoicing;
using RRBridal.StoreBilling.App.Services.Masters;
using RRBridal.StoreBilling.App.Services.Api;
using RRBridal.StoreBilling.App.Services.Sync;

namespace RRBridal.StoreBilling.App.Services;

public sealed class AppServices
{
    public IFocusSearchService? FocusSearch { get; set; }

    public required IMongoDatabase LocalDb { get; init; }
    public required HttpClient CentralApi { get; init; }
    public required ISyncEngine SyncEngine { get; init; }
    public required IPaymentRouter PaymentRouter { get; init; }
    public required PurchaseIntentPublisher PurchaseIntentPublisher { get; init; }
    public required ProductImageCache ProductImageCache { get; init; }
    public required ProductCatalogService ProductCatalog { get; init; }
    public required InventoryGridClient InventoryGrid { get; init; }
    public required CentralAuthSession CentralAuthSession { get; init; }
    public required CentralAuthClient CentralAuthClient { get; init; }

    public required MasterDataService MasterData { get; init; }
    public required LocalAuthService LocalAuth { get; init; }
    public required ReceiptConfigStore ReceiptConfig { get; init; }
    public required ReceiptLogoCache ReceiptLogoCache { get; init; }
    public required ReceiptConfigSyncService ReceiptConfigSync { get; init; }
    public required StoreContext StoreContext { get; init; }
    public UserSession? UserSession { get; set; }

    public static AppServices CreateDefault()
    {
        var storeContext = new StoreContext();
        var localMongoUri = Environment.GetEnvironmentVariable("STORE_MONGO_URI") ?? "mongodb://localhost:27017/rr_bridal_store";
        var centralApiBase = Environment.GetEnvironmentVariable("CENTRAL_API_BASE") ?? "http://localhost:3000";
 //var centralApiBase = Environment.GetEnvironmentVariable("CENTRAL_API_BASE") ?? "http://bridaldev.rrbazaar.in";
        var mongoClient = new MongoClient(localMongoUri);
        var localDb = mongoClient.GetDatabase(new MongoUrl(localMongoUri).DatabaseName ?? "rr_bridal_store");

        var authSession = new CentralAuthSession();
        authSession.LoadFromDisk();

        var http = new HttpClient()
        {
            BaseAddress = new Uri(centralApiBase),
            Timeout = TimeSpan.FromSeconds(30),
        };
        authSession.ApplyTo(http);

        var centralAuthClient = new CentralAuthClient(http, authSession);

        var paymentRouter = new PaymentRouter(
            new PineLabsPaymentProvider(),
            new RazorpayPaymentProvider(),
            localDb,
            storeContext);

        var masterData = new MasterDataService(localDb, http);
        var localAuth = new LocalAuthService(localDb);
        var purchaseIntentPublisher = new PurchaseIntentPublisher(localDb, storeContext);
        var productImageCache = new ProductImageCache(http);
        var productCatalog = new ProductCatalogService(localDb, http);
        var inventoryGrid = new InventoryGridClient(localDb);
        var receiptConfig = new ReceiptConfigStore();
        var receiptLogoCache = new ReceiptLogoCache(http);
        var companyProfileClient = new CompanyProfileClient(http);
        var storeReceiptClient = new StoreReceiptSettingsClient(http);
        var receiptConfigSync = new ReceiptConfigSyncService(
            companyProfileClient,
            storeReceiptClient,
            receiptConfig,
            receiptLogoCache,
            storeContext,
            authSession,
            http);

        var syncEngine = new SyncEngine(localDb, http, storeContext, masterData, receiptConfigSync);

        return new AppServices
        {
            LocalDb = localDb,
            CentralApi = http,
            SyncEngine = syncEngine,
            PaymentRouter = paymentRouter,
            PurchaseIntentPublisher = purchaseIntentPublisher,
            ProductImageCache = productImageCache,
            ProductCatalog = productCatalog,
            InventoryGrid = inventoryGrid,
            CentralAuthSession = authSession,
            CentralAuthClient = centralAuthClient,
            MasterData = masterData,
            LocalAuth = localAuth,
            ReceiptConfig = receiptConfig,
            ReceiptLogoCache = receiptLogoCache,
            ReceiptConfigSync = receiptConfigSync,
            StoreContext = storeContext,
        };
    }
}

