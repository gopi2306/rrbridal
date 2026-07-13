import { join } from 'path';
import { Module } from '@nestjs/common';
import { ConfigModule } from '@nestjs/config';
import { MongooseModule } from '@nestjs/mongoose';
import { ServeStaticModule } from '@nestjs/serve-static';
import { AppController } from './app.controller';
import { AuthModule } from '../auth/auth.module';
import { UsersModule } from '../users/users.module';
import { SyncModule } from '../sync/sync.module';
import { ProductsModule } from '../products/products.module';
import { MediaModule } from '../media/media.module';
import { CustomersModule } from '../customers/customers.module';
import { SalesmenModule } from '../salesmen/salesmen.module';
import { SuppliersModule } from '../suppliers/suppliers.module';
import { PurchaseOrdersModule } from '../purchase-orders/purchase-orders.module';
import { PurchaseReturnsModule } from '../purchase-returns/purchase-returns.module';
import { InventoryModule } from '../inventory/inventory.module';
import { GoodsReceiptsModule } from '../goods-receipts/goods-receipts.module';
import { PurchaseIntentsModule } from '../purchase-intents/purchase-intents.module';
import { StockTransfersModule } from '../stock-transfers/stock-transfers.module';
import { StoresModule } from '../stores/stores.module';
import { ManufacturersModule } from '../manufacturers/manufacturers.module';
import { DepartmentsModule } from '../departments/departments.module';
import { CategoriesModule } from '../categories/categories.module';
import { SubCategoriesModule } from '../sub-categories/sub-categories.module';
import { BrandsModule } from '../brands/brands.module';
import { WeightSizesModule } from '../weight-sizes/weight-sizes.module';
import { WeightUnitsModule } from '../weight-units/weight-units.module';
import { OfferGroupsModule } from '../offer-groups/offer-groups.module';
import { ProductStatusesModule } from '../product-statuses/product-statuses.module';
import { ColoursModule } from '../colours/colours.module';
import { HsnCodesModule } from '../hsn-codes/hsn-codes.module';
import { GstUomsModule } from '../gst-uoms/gst-uoms.module';
import { UomSubsModule } from '../uom-subs/uom-subs.module';
import { BatchExpiryDetailsModule } from '../batch-expiry-details/batch-expiry-details.module';
import { ItemPrepStatusesModule } from '../item-prep-statuses/item-prep-statuses.module';
import { PackedConfirmationsModule } from '../packed-confirmations/packed-confirmations.module';
import { PoQtyPoliciesModule } from '../po-qty-policies/po-qty-policies.module';
import { SellByTypesModule } from '../sell-by-types/sell-by-types.module';
import { BatchSelectionsModule } from '../batch-selections/batch-selections.module';
import { SkuTypesModule } from '../sku-types/sku-types.module';
import { SkuOrderGroupsModule } from '../sku-order-groups/sku-order-groups.module';
import { IndentTypesModule } from '../indent-types/indent-types.module';
import { DashboardModule } from '../dashboard/dashboard.module';
import { CompanyProfileModule } from '../company-profile/company-profile.module';
import { ResourceLimitsModule } from '../resource-limits/resource-limits.module';
import { BranchesModule } from '../branches/branches.module';
import { DivisionsModule } from '../divisions/divisions.module';
import { LocationsModule } from '../locations/locations.module';
import { SeedModule } from '../seed/seed.module';
import { RoleAccessModule } from '../role-access/role-access.module';
import { DocumentNumbersModule } from '../document-numbers/document-numbers.module';
import { StoreSalesModule } from '../store-sales/store-sales.module';
import { PromotionSchemesModule } from '../promotion-schemes/promotion-schemes.module';
import { MyStoreModule } from '../my-store/my-store.module';
import { MyWarehouseModule } from '../my-warehouse/my-warehouse.module';
import { AuditLogsModule } from '../audit-logs/audit-logs.module';
import { StockAuditModule } from '../stock-audit/stock-audit.module';
import { StockTallyModule } from '../stock-tally/stock-tally.module';
import { InventoryAdjustmentsModule } from '../inventory-adjustments/inventory-adjustments.module';
import { BillsModule } from '../bills/bills.module';
import { WhatsAppModule } from '../whatsapp/whatsapp.module';
import { ReportsModule } from '../reports/reports.module';
import { BarcodeLabelDesignsModule } from '../barcode-label-designs/barcode-label-design.module';


@Module({
  imports: [
    ConfigModule.forRoot({
      isGlobal: true,
    }),
    ServeStaticModule.forRoot({
      rootPath: join(__dirname, '..', '..', '..', 'build'),
      exclude: ['/api{/*path}'],
    }),
    MongooseModule.forRoot(process.env.MONGO_URI ?? 'mongodb://localhost:27017/rr_bridal_central'),
    StoresModule,
    UsersModule,
    AuthModule,
    SyncModule,
    ProductsModule,
    MediaModule,
    CustomersModule,
    SalesmenModule,
    SuppliersModule,
    PurchaseOrdersModule,
    PurchaseReturnsModule,
    InventoryModule,
    GoodsReceiptsModule,
    PurchaseIntentsModule,
    StockTransfersModule,
    ManufacturersModule,
    DepartmentsModule,
    CategoriesModule,
    SubCategoriesModule,
    BrandsModule,
    WeightSizesModule,
    WeightUnitsModule,
    OfferGroupsModule,
    ProductStatusesModule,
    ColoursModule,
    HsnCodesModule,
    GstUomsModule,
    UomSubsModule,
    BatchExpiryDetailsModule,
    ItemPrepStatusesModule,
    PackedConfirmationsModule,
    PoQtyPoliciesModule,
    SellByTypesModule,
    BatchSelectionsModule,
    SkuTypesModule,
    SkuOrderGroupsModule,
    IndentTypesModule,
    DashboardModule,
    CompanyProfileModule,
    ResourceLimitsModule,
    BranchesModule,
    DivisionsModule,
    LocationsModule,
    SeedModule,
    RoleAccessModule,
    DocumentNumbersModule,
    StoreSalesModule,
    PromotionSchemesModule,
    MyStoreModule,
    MyWarehouseModule,
    AuditLogsModule,

    StockAuditModule,
    StockTallyModule,
    InventoryAdjustmentsModule,
    BillsModule,
    WhatsAppModule,
    ReportsModule,
    BarcodeLabelDesignsModule,
  ],
  controllers: [AppController],
})
export class AppModule {}
