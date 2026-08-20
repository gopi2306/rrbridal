using System;
using System.Net.Http;
using MongoDB.Bson;
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
using RRBridal.StoreBilling.App.Services.BarcodePrinting;
using RRBridal.StoreBilling.App.Services.Audit;
using RRBridal.StoreBilling.App.Services.Store;
using RRBridal.StoreBilling.App.Services.Ui;
using RRBridal.StoreBilling.App.Services.WhatsApp;
using RRBridal.StoreBilling.App.Services.Expenses;
using RRBridal.StoreBilling.App.Services.Billing.Promotions;
using RRBridal.StoreBilling.App.Services.Dispatch;

namespace RRBridal.StoreBilling.App.Services;

public sealed class AppServices
{
    public IFocusSearchService? FocusSearch { get; set; }

    /// <summary>Focuses billing line-item product search (set by BillingView).</summary>
    public Action? FocusBillingProductSearch { get; set; }

    /// <summary>Focuses barcode printing draft SKU row (set by BarcodePrintingView).</summary>
    public Action? FocusBarcodeSkuEntry { get; set; }

    public required IMongoDatabase LocalDb { get; init; }
    public required StoreMongoOptions StoreMongoOptions { get; init; }
    public required MongoHealthMonitor MongoHealth { get; init; }
    public required HttpClient CentralApi { get; init; }
    public required ISyncEngine SyncEngine { get; init; }
    public required IPaymentRouter PaymentRouter { get; init; }
    public required PurchaseIntentPublisher PurchaseIntentPublisher { get; init; }
    public required ProductImageCache ProductImageCache { get; init; }
    public required ProductCatalogService ProductCatalog { get; init; }
    public required InventoryGridClient InventoryGrid { get; init; }
    public required PhysicalInventoryExcelService PhysicalInventoryExcel { get; init; }
    public required InventoryAdjustmentService InventoryAdjustments { get; init; }
    public required CentralAuthSession CentralAuthSession { get; init; }
    public required CentralAuthClient CentralAuthClient { get; init; }

    public required MasterDataService MasterData { get; init; }
    public required PromotionSchemeRepository PromotionSchemes { get; init; }
    public required LocalAuthService LocalAuth { get; init; }
    public required ReceiptConfigStore ReceiptConfig { get; init; }
    public required ReceiptLogoCache ReceiptLogoCache { get; init; }
    public required ReceiptConfigSyncService ReceiptConfigSync { get; init; }
    public required BarcodeLabelDesignStore BarcodeLabelDesign { get; init; }
    public required BarcodeLabelDesignSyncService BarcodeLabelDesignSync { get; init; }
    public required StoreContext StoreContext { get; init; }
    public required BillNumberGenerator BillNumberGenerator { get; init; }
    public required BillingOutboxPublisher BillingOutbox { get; init; }
    public required PosBillingSettingsStore PosBillingSettings { get; init; }
    public required ShellUiSettingsStore ShellUiSettings { get; init; }
    public required RazorpayPosSettingsStore RazorpayPosSettings { get; init; }
    public required BillDocumentService BillDocuments { get; init; }
    public required BillDeleteService BillDelete { get; init; }
    public required HeldBillService HeldBills { get; init; }
    public required CustomerCreditNoteService CustomerCreditNotes { get; init; }
    public required SaleReturnHistoryService SaleReturnHistory { get; init; }
    public required ShellBrandingService ShellBranding { get; init; }
    public required StoreInfoClient StoreInfo { get; init; }
    public required StoreSyncRunner StoreSyncRunner { get; init; }
    public required PeriodicSyncService PeriodicSync { get; init; }
    public required CentralOnlineModeService CentralMode { get; init; }
    public required CentralStorePosClient StorePos { get; init; }
    public required CentralDashboardClient DashboardApi { get; init; }
    public required OutboxNotificationService OutboxNotifications { get; init; }
    public required StoreAuditLogService StoreAuditLog { get; init; }
    public required StoreBillListService StoreBillList { get; init; }
    public required CustomerBillingReportService CustomerBillingReports { get; init; }
    public required SkuSalesReportService SkuSalesReports { get; init; }
    public required GoingOutOfStockReportService GoingOutOfStockReports { get; init; }
    public required DaySessionService DaySessions { get; init; }
    public required DayCloseReportService DayCloseReports { get; init; }
    public required CashMovementService CashMovements { get; init; }
    public required DailyExpenseService DailyExpenses { get; init; }
    public required OutboundDispatchService OutboundDispatches { get; init; }
    public required OnlineCodBillService OnlineCodBills { get; init; }
    public required QuotationService Quotations { get; init; }
    public required CreditBillService CreditBills { get; init; }
    public required WhatsAppBillService WhatsAppBills { get; init; }
    public required WhatsAppLocalPreferencesStore WhatsAppPreferences { get; init; }
    public required WhatsAppSettingsClient WhatsAppClient { get; init; }
    public UserSession? UserSession { get; set; }

    public Action? NotifyDaySessionChanged { get; set; }

    public static AppServices CreateDefault()
    {
        var storeContext = new StoreContext();
        var storeMongoOptions = new StoreMongoOptions();
        var centralApiBase = Environment.GetEnvironmentVariable("CENTRAL_API_BASE") ?? "http://localhost:3000";
        var mongoSettings = MongoClientSettings.FromConnectionString(storeMongoOptions.ConnectionUri);
        mongoSettings.ConnectTimeout = storeMongoOptions.ConnectTimeout;
        mongoSettings.ServerSelectionTimeout = storeMongoOptions.ServerSelectionTimeout;
        if (storeMongoOptions.SocketTimeout.HasValue)
            mongoSettings.SocketTimeout = storeMongoOptions.SocketTimeout.Value;
        mongoSettings.RetryReads = true;
        mongoSettings.RetryWrites = true;
        var mongoClient = new MongoClient(mongoSettings);
        var localDb = mongoClient.GetDatabase(
            new MongoUrl(storeMongoOptions.ConnectionUri).DatabaseName ?? "rr_bridal_store");
        var mongoHealth = new MongoHealthMonitor(localDb, storeMongoOptions);

        var authSession = new CentralAuthSession();
        authSession.LoadFromDisk();

        var http = new HttpClient()
        {
            BaseAddress = new Uri(centralApiBase),
            Timeout = TimeSpan.FromSeconds(30),
        };
        authSession.ApplyTo(http);

        var centralAuthClient = new CentralAuthClient(http, authSession);

        var razorpayPosSettings = new RazorpayPosSettingsStore();
        var paymentRouter = new PaymentRouter(
            new PineLabsPaymentProvider(),
            new RazorpayPaymentProvider(razorpayPosSettings),
            localDb,
            storeContext);

        var masterData = new MasterDataService(localDb, http);
        var localAuth = new LocalAuthService(localDb, storeContext);
        var productImageCache = new ProductImageCache(http);
        var storeAuditLog = new StoreAuditLogService(localDb, storeContext);
        var posBillingSettings = new PosBillingSettingsStore();
        var billingOutbox = new BillingOutboxPublisher(localDb, storeContext);
        var purchaseIntentPublisher = new PurchaseIntentPublisher(localDb, storeContext, billingOutbox);
        var storePos = new CentralStorePosClient(http, storeContext);
        var dashboardApi = new CentralDashboardClient(http, storeContext);
        var productCatalog = new ProductCatalogService(localDb, http, storeAuditLog);
        var inventoryGrid = new InventoryGridClient(localDb);
        var physicalInventoryExcel = new PhysicalInventoryExcelService(http, storeContext, inventoryGrid);
        var inventoryAdjustments = new InventoryAdjustmentService(localDb, productCatalog, billingOutbox, storeContext);
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
        var barcodeLabelDesign = new BarcodeLabelDesignStore();
        var barcodeLabelDesignClient = new BarcodeLabelDesignClient(http);
        var barcodeLabelDesignSync = new BarcodeLabelDesignSyncService(
            barcodeLabelDesignClient,
            barcodeLabelDesign,
            authSession);
        var promotionSchemes = new PromotionSchemeRepository(localDb);

        var syncEngine = new SyncEngine(
            localDb,
            http,
            storeContext,
            masterData,
            receiptConfigSync,
            barcodeLabelDesignSync,
            storeAuditLog,
            inventoryAdjustments);
        var billNumberGenerator = new BillNumberGenerator(localDb, storeContext);
        var shellUiSettings = new ShellUiSettingsStore();
        var billDocuments = new BillDocumentService(localDb, storeContext, receiptConfig);
        var storeBillList = new StoreBillListService(localDb);
        var customerBillingReports = new CustomerBillingReportService(localDb, http, storeContext);
        var skuSalesReports = new SkuSalesReportService(localDb, http, storeContext);
        var goingOutOfStockReports = new GoingOutOfStockReportService(localDb, http, storeContext);
        var billDelete = new BillDeleteService(
            localDb, storeContext, billDocuments, storeBillList, productCatalog, billingOutbox);
        var heldBills = new HeldBillService(localDb, storeContext, billNumberGenerator);
        var customerCreditNotes = new CustomerCreditNoteService(localDb, billingOutbox);
        var saleReturnHistory = new SaleReturnHistoryService(localDb);
        var daySessions = new DaySessionService(localDb, productCatalog, billingOutbox, storeContext, storeAuditLog);
        var dayCloseReports = new DayCloseReportService(localDb, daySessions, storeBillList);
        var cashMovements = new CashMovementService(localDb, billNumberGenerator, billingOutbox, storeContext, daySessions);
        var dailyExpenses = new DailyExpenseService(localDb, storeContext, billNumberGenerator, billingOutbox, storeAuditLog);
        var outboundDispatches = new OutboundDispatchService(
            localDb, storeContext, billNumberGenerator, billingOutbox, dailyExpenses, storeAuditLog);
        var onlineCodBills = new OnlineCodBillService(localDb, billingOutbox);
        var quotations = new QuotationService(localDb, storeContext, billNumberGenerator, billingOutbox);
        var creditBills = new CreditBillService(localDb, billingOutbox, billNumberGenerator);
        var whatsappPrefs = new WhatsAppLocalPreferencesStore();
        var whatsappClient = new WhatsAppSettingsClient(http);
        var syncSchedule = new SyncScheduleOptions();
        AppServices? servicesRef = null;
        var whatsappBills = new WhatsAppBillService(localDb, storeContext, whatsappClient, whatsappPrefs, () => servicesRef!);
        var onlineDirectOperations = new OnlineDirectOperationsRunner(
            storeContext,
            storePos,
            masterData,
            receiptConfigSync,
            barcodeLabelDesignSync,
            shellBranding,
            promotionSchemes);
        var storeSyncRunner = new StoreSyncRunner(
            syncEngine,
            authSession,
            http,
            receiptConfigSync,
            shellBranding,
            localAuth,
            () => servicesRef?.UserSession,
            () => servicesRef?.CentralMode.IsOnlineMode ?? posBillingSettings.Current.PreferCentralOnline,
            onlineDirectOperations);
        var centralMode = new CentralOnlineModeService(
            posBillingSettings,
            http,
            storeSyncRunner,
            storeInfoClient,
            receiptConfig,
            receiptConfigSync,
            storeContext.StoreId);
        masterData.ConfigureOnline(() => centralMode.IsOnlineMode);
        promotionSchemes.ConfigureOnline(centralMode, storePos);
        productCatalog.ConfigureOnline(centralMode, storePos);
        inventoryGrid.ConfigureOnline(centralMode, dashboardApi);
        physicalInventoryExcel.ConfigureOnline(centralMode);
        inventoryAdjustments.ConfigureOnline(centralMode);
        billNumberGenerator.ConfigureOnline(centralMode, storePos);
        billDocuments.ConfigureOnline(centralMode, storePos);
        heldBills.ConfigureOnline(centralMode, storePos);
        quotations.ConfigureOnline(centralMode, storePos);
        billDelete.ConfigureOnline(centralMode);
        daySessions.ConfigureOnline(centralMode, storePos, dashboardApi);
        dayCloseReports.ConfigureOnline(centralMode, dashboardApi, storePos);
        cashMovements.ConfigureOnline(centralMode, storePos);
        onlineCodBills.ConfigureOnline(centralMode, billDocuments, storePos);
        creditBills.ConfigureOnline(centralMode, billDocuments, storePos);
        customerCreditNotes.ConfigureOnline(centralMode, storePos);
        customerCreditNotes.ConfigureNumberGenerator(billNumberGenerator);
        saleReturnHistory.ConfigureOnline(centralMode, storePos);
        storeBillList.ConfigureOnline(centralMode, storePos);
        customerBillingReports.ConfigureOnline(centralMode);
        skuSalesReports.ConfigureOnline(centralMode);
        goingOutOfStockReports.ConfigureOnline(centralMode);
        outboundDispatches.ConfigureOnline(centralMode, storePos);
        paymentRouter.ConfigureOnline(centralMode, storePos);
        var periodicSync = new PeriodicSyncService(storeContext, syncSchedule, storeSyncRunner, localDb, shellBranding, centralMode);
        var outboxNotifications = new OutboxNotificationService(localDb, storeContext);

        try { _ = StoreIndexEnsurer.EnsureAsync(localDb); } catch { /* best-effort index */ }
        try { _ = heldBills.MigrateDraftsFromStoreBillsAsync(); } catch { /* best-effort migration */ }

        servicesRef = new AppServices
        {
            LocalDb = localDb,
            StoreMongoOptions = storeMongoOptions,
            MongoHealth = mongoHealth,
            CentralApi = http,
            SyncEngine = syncEngine,
            PaymentRouter = paymentRouter,
            PurchaseIntentPublisher = purchaseIntentPublisher,
            ProductImageCache = productImageCache,
            ProductCatalog = productCatalog,
            InventoryGrid = inventoryGrid,
            PhysicalInventoryExcel = physicalInventoryExcel,
            InventoryAdjustments = inventoryAdjustments,
            CentralAuthSession = authSession,
            CentralAuthClient = centralAuthClient,
            MasterData = masterData,
            PromotionSchemes = promotionSchemes,
            LocalAuth = localAuth,
            ReceiptConfig = receiptConfig,
            ReceiptLogoCache = receiptLogoCache,
            ReceiptConfigSync = receiptConfigSync,
            BarcodeLabelDesign = barcodeLabelDesign,
            BarcodeLabelDesignSync = barcodeLabelDesignSync,
            StoreContext = storeContext,
            BillNumberGenerator = billNumberGenerator,
            BillingOutbox = billingOutbox,
            PosBillingSettings = posBillingSettings,
            ShellUiSettings = shellUiSettings,
            RazorpayPosSettings = razorpayPosSettings,
            BillDocuments = billDocuments,
            BillDelete = billDelete,
            HeldBills = heldBills,
            CustomerCreditNotes = customerCreditNotes,
            SaleReturnHistory = saleReturnHistory,
            ShellBranding = shellBranding,
            StoreInfo = storeInfoClient,
            StoreSyncRunner = storeSyncRunner,
            PeriodicSync = periodicSync,
            CentralMode = centralMode,
            StorePos = storePos,
            DashboardApi = dashboardApi,
            OutboxNotifications = outboxNotifications,
            StoreAuditLog = storeAuditLog,
            StoreBillList = storeBillList,
            CustomerBillingReports = customerBillingReports,
            SkuSalesReports = skuSalesReports,
            GoingOutOfStockReports = goingOutOfStockReports,
            DaySessions = daySessions,
            DayCloseReports = dayCloseReports,
            CashMovements = cashMovements,
            DailyExpenses = dailyExpenses,
            OutboundDispatches = outboundDispatches,
            OnlineCodBills = onlineCodBills,
            Quotations = quotations,
            CreditBills = creditBills,
            WhatsAppBills = whatsappBills,
            WhatsAppPreferences = whatsappPrefs,
            WhatsAppClient = whatsappClient,
        };
        billingOutbox.ConfigureOnlineDispatch(
            () => servicesRef.CentralMode.IsOnlineMode,
            ct => syncEngine.PushPendingAsync(ct),
            async (type, payload, hash, eventId, ct) =>
            {
                var mapped = BsonTypeMapper.MapToDotNetValue(payload);
                var storePos = servicesRef.StorePos;
                switch (type)
                {
                    case "InvoiceCreated":
                        await storePos.PostBillAsync(mapped!, ct).ConfigureAwait(false);
                        break;
                    case "InvoiceDeleted":
                    {
                        var billNo = ReadPayloadString(payload, "billNo")
                                     ?? ReadPayloadString(payload, "invoiceNo")
                                     ?? "";
                        await storePos.DeleteBillAsync(billNo, mapped, ct).ConfigureAwait(false);
                        break;
                    }
                    case "SaleReturnCreated":
                        await storePos.PostSaleReturnAsync(mapped!, exchange: false, ct).ConfigureAwait(false);
                        break;
                    case "SaleExchangeCreated":
                        await storePos.PostSaleReturnAsync(mapped!, exchange: true, ct).ConfigureAwait(false);
                        break;
                    case "QuotationUpserted":
                        await storePos.PostQuotationAsync(mapped!, ct).ConfigureAwait(false);
                        break;
                    case "QuotationConverted":
                        await storePos.ConvertQuotationAsync(mapped!, ct).ConfigureAwait(false);
                        break;
                    case "QuotationCancelled":
                        await storePos.CancelQuotationAsync(mapped!, ct).ConfigureAwait(false);
                        break;
                    case "CreditNoteCreated":
                        await storePos.PostCreditNoteAsync(mapped!, ct).ConfigureAwait(false);
                        break;
                    case "CreditNoteApplied":
                        await storePos.ApplyCreditNoteAsync(mapped!, ct).ConfigureAwait(false);
                        break;
                    case "CreditNoteCashedOut":
                        await storePos.CashoutCreditNoteAsync(mapped!, ct).ConfigureAwait(false);
                        break;
                    case "DaySessionOpened":
                        await storePos.OpenDaySessionAsync(mapped!, ct).ConfigureAwait(false);
                        break;
                    case "DaySessionClosed":
                        await storePos.CloseDaySessionAsync(mapped!, ct).ConfigureAwait(false);
                        break;
                    case "CashMovementCreated":
                        await storePos.PostCashMovementAsync(mapped!, ct).ConfigureAwait(false);
                        break;
                    case "DailyExpenseCreated":
                        await storePos.PostDailyExpenseAsync(mapped!, ct).ConfigureAwait(false);
                        break;
                    case "DailyExpenseUpdated":
                    {
                        var expenseNo = ReadPayloadString(payload, "expenseNo") ?? "";
                        await storePos.UpdateDailyExpenseAsync(expenseNo, mapped!, ct).ConfigureAwait(false);
                        break;
                    }
                    case "DailyExpenseVoided":
                    {
                        var expenseNo = ReadPayloadString(payload, "expenseNo") ?? "";
                        await storePos.VoidDailyExpenseAsync(expenseNo, mapped!, ct).ConfigureAwait(false);
                        break;
                    }
                    case "OutboundDispatchCreated":
                        await storePos.CreateOutboundDispatchAsync(mapped!, ct).ConfigureAwait(false);
                        break;
                    case "OutboundDispatchUpdated":
                    {
                        var dispatchNo = ReadPayloadString(payload, "dispatchNo") ?? "";
                        await storePos.UpdateOutboundDispatchAsync(dispatchNo, mapped!, ct).ConfigureAwait(false);
                        break;
                    }
                    case "OutboundDispatchStatusChanged":
                    {
                        var dispatchNo = ReadPayloadString(payload, "dispatchNo") ?? "";
                        await storePos.ChangeOutboundDispatchStatusAsync(dispatchNo, mapped!, ct).ConfigureAwait(false);
                        break;
                    }
                    case "OutboundDispatchChargeReceived":
                    {
                        var dispatchNo = ReadPayloadString(payload, "dispatchNo") ?? "";
                        await storePos.ReceiveOutboundDispatchChargeAsync(dispatchNo, mapped!, ct).ConfigureAwait(false);
                        break;
                    }
                    case "OutboundDispatchCancelled":
                    {
                        var dispatchNo = ReadPayloadString(payload, "dispatchNo") ?? "";
                        await storePos.CancelOutboundDispatchAsync(dispatchNo, mapped!, ct).ConfigureAwait(false);
                        break;
                    }
                    case "InvoiceCodPaymentReceived":
                    {
                        var billNo = ReadPayloadString(payload, "billNo") ?? "";
                        await storePos.PostCodPaymentAsync(billNo, mapped!, ct).ConfigureAwait(false);
                        break;
                    }
                    case "InvoiceCreditPaymentReceived":
                    {
                        var billNo = ReadPayloadString(payload, "billNo") ?? "";
                        await storePos.PostCreditPaymentAsync(billNo, mapped!, ct).ConfigureAwait(false);
                        break;
                    }
                    case "AdjustmentBillCreated":
                        await storePos.PostAdjustmentBillAsync(mapped!, ct).ConfigureAwait(false);
                        break;
                    default:
                        await storePos.PostEventAsync(type, mapped!, ct, eventId).ConfigureAwait(false);
                        break;
                }
            });
        return servicesRef;
    }

    private static string? ReadPayloadString(BsonDocument payload, string field)
    {
        if (!payload.Contains(field) || payload[field].IsBsonNull)
            return null;
        return payload[field].ToString();
    }
}

