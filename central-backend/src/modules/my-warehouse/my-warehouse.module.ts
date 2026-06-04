import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { GoodsReceipt, GoodsReceiptSchema } from '../goods-receipts/schemas/goods-receipt.schema';
import { InventoryModule } from '../inventory/inventory.module';
import { ProductsModule } from '../products/products.module';
import { Location, LocationSchema } from '../locations/schemas/location.schema';
import { Product, ProductSchema } from '../products/schemas/product.schema';
import { PurchaseIntent, PurchaseIntentSchema } from '../purchase-intents/schemas/purchase-intent.schema';
import { PurchaseOrder, PurchaseOrderSchema } from '../purchase-orders/schemas/purchase-order.schema';
import { StockTransfer, StockTransferSchema } from '../stock-transfers/schemas/stock-transfer.schema';
import { Store, StoreSchema } from '../stores/schemas/store.schema';
import { MyWarehouseController } from './my-warehouse.controller';
import { MyWarehouseService } from './my-warehouse.service';

@Module({
  imports: [
    InventoryModule,
    ProductsModule,
    MongooseModule.forFeature([
      { name: Location.name, schema: LocationSchema },
      { name: GoodsReceipt.name, schema: GoodsReceiptSchema },
      { name: PurchaseOrder.name, schema: PurchaseOrderSchema },
      { name: StockTransfer.name, schema: StockTransferSchema },
      { name: PurchaseIntent.name, schema: PurchaseIntentSchema },
      { name: Store.name, schema: StoreSchema },
      { name: Product.name, schema: ProductSchema },
    ]),
  ],
  controllers: [MyWarehouseController],
  providers: [MyWarehouseService],
})
export class MyWarehouseModule {}
