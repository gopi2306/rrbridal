import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { Category, CategorySchema } from '../categories/schemas/category.schema';
import { PurchaseOrder, PurchaseOrderSchema } from '../purchase-orders/schemas/purchase-order.schema';
import { GoodsReceipt, GoodsReceiptSchema } from '../goods-receipts/schemas/goods-receipt.schema';
import { StockTransfer, StockTransferSchema } from '../stock-transfers/schemas/stock-transfer.schema';
import { Supplier, SupplierSchema } from '../suppliers/schemas/supplier.schema';
import { InventoryModule } from '../inventory/inventory.module';
import { InventoryLedgerEntry, InventoryLedgerSchema } from '../inventory/schemas/inventory-ledger.schema';
import { Location, LocationSchema } from '../locations/schemas/location.schema';
import { Product, ProductSchema } from '../products/schemas/product.schema';
import { Store, StoreSchema } from '../stores/schemas/store.schema';
import { DashboardController } from './dashboard.controller';
import { DashboardService } from './dashboard.service';
import { PurchaseIntent, PurchaseIntentSchema } from '../purchase-intents/schemas/purchase-intent.schema';
import { StoreCreditNote, StoreCreditNoteSchema } from '../store-sales/schemas/store-credit-note.schema';
import { StoreInvoice, StoreInvoiceSchema } from '../store-sales/schemas/store-invoice.schema';
import { StoreSaleReturn, StoreSaleReturnSchema } from '../store-sales/schemas/store-sale-return.schema';
import { StoreDashboardService } from './store-dashboard.service';
import { StoreSalesDashboardService } from './store-sales-dashboard.service';
import { WarehouseDashboardService } from './warehouse-dashboard.service';

@Module({
  imports: [
    InventoryModule,
    MongooseModule.forFeature([
      { name: PurchaseOrder.name, schema: PurchaseOrderSchema },
      { name: GoodsReceipt.name, schema: GoodsReceiptSchema },
      { name: StockTransfer.name, schema: StockTransferSchema },
      { name: Supplier.name, schema: SupplierSchema },
      { name: InventoryLedgerEntry.name, schema: InventoryLedgerSchema },
      { name: Product.name, schema: ProductSchema },
      { name: Category.name, schema: CategorySchema },
      { name: Location.name, schema: LocationSchema },
      { name: Store.name, schema: StoreSchema },
      { name: PurchaseIntent.name, schema: PurchaseIntentSchema },
      { name: StoreInvoice.name, schema: StoreInvoiceSchema },
      { name: StoreSaleReturn.name, schema: StoreSaleReturnSchema },
      { name: StoreCreditNote.name, schema: StoreCreditNoteSchema },
    ]),
  ],
  controllers: [DashboardController],
  providers: [
    DashboardService,
    WarehouseDashboardService,
    StoreDashboardService,
    StoreSalesDashboardService,
  ],
})
export class DashboardModule {}
