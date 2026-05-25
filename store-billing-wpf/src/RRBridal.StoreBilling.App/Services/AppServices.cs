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
using RRBridal.StoreBilling.App.Services.Notifications;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Sync;

namespace RRBridal.StoreBilling.App.Services;

public sealed class AppServices
{
    public IFocusSearchService? FocusSearch { get; set; }

    /// <summary>Focuses billing line-item product search (set by BillingView).</summary>
    public Action? FocusBillingProductSearch { get; set; }

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
    public required BillNumberGenerator BillNumberGenerator { get; init; }
    public required PosBillingSettingsStore PosBillingSettings { get; init; }
    public required BillDocumentService BillDocuments { get; init; }
    public required ShellBrandingService ShellBranding { get; init; }
    public required StoreInfoClient StoreInfo { get; init; }
    public required StoreSyncRunner StoreSyncRunner { get; init; }
    public required PeriodicSyncService PeriodicSync { get; init; }
    public required OutboxNotificationService OutboxNotifications { get; init; }
    public UserSession? UserSession { get; set; }

    public static AppServices CreateDefault()
    {
        var storeContext = new StoreContext();
        var localMongoUri = Environment.GetEnvironmentVariable("STORE_MONGO_URI") ?? "mongodb://localhost:27017/rr_bridal_store";
        var centralApiBase = Environment.GetEnvironmentVariable("CENTRAL_API_BASE") ?? "http://localhost:3000";
        var mongoSettings = MongoClientSettings.FromConnectionString(localMongoUri);
        mongoSettings.ConnectTimeout = TimeSpan.FromSeconds(5);
        mongoSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        var mongoClient = new MongoClient(mongoSettings);
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
        var localAuth = new LocalAuthService(localDb, storeContext);
        var purchaseIntentPublisher = new PurchaseIntentPublisher(localDb, storeContext);
        var productImageCache = new ProductImageCache(http);
        var productCatalog = new ProductCatalogService(localDb, http);
        var inventoryGrid = new InventoryGridClient(localDb);
        var receiptConfig = new ReceiptConfigStore();
        var receiptLogoCache = new ReceiptLogoCache(http);
        var companyProfileClient = new CompanyProfileClient(http);
        var storeReceiptClient = new StoreReceiptSettingsClient(http);
        var storeInfoClient = new StoreInfoClient(http);
        var shellBranding = new ShellBrandingService(
            receiptConfig,
            storeContext,
            storeInfoClient,
            authSession,
            localDb);
        var receiptConfigSync = new ReceiptConfigSyncService(
            companyProfileClient,
            storeReceiptClient,
            receiptConfig,
            receiptLogoCache,
            storeContext,
            authSession,
            http);

        var syncEngine = new SyncEngine(localDb, http, storeContext, masterData, receiptConfigSync);
        var billNumberGenerator = new BillNumberGenerator(localDb, storeContext);
        var posBillingSettings = new PosBillingSettingsStore();
        var billDocuments = new BillDocumentService(localDb, storeContext, receiptConfig);
        var syncSchedule = new SyncScheduleOptions();
        var storeSyncRunner = new StoreSyncRunner(syncEngine, authSession, http, receiptConfigSync, shellBranding);
        var periodicSync = new PeriodicSyncService(storeContext, syncSchedule, storeSyncRunner, localDb, shellBranding);
        var outboxNotifications = new OutboxNotificationService(localDb, storeContext);

        try { _ = StoreIndexEnsurer.EnsureAsync(localDb); } catch { /* best-effort index */ }

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
            BillNumberGenerator = billNumberGenerator,
            PosBillingSettings = posBillingSettings,
            BillDocuments = billDocuments,
            ShellBranding = shellBranding,
            StoreInfo = storeInfoClient,
            StoreSyncRunner = storeSyncRunner,
            PeriodicSync = periodicSync,
            OutboxNotifications = outboxNotifications,
        };
    }
}

