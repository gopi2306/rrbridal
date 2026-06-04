import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { InventoryModule } from '../inventory/inventory.module';
import { ProductsModule } from '../products/products.module';
import { Product, ProductSchema } from '../products/schemas/product.schema';
import { PurchaseIntent, PurchaseIntentSchema } from '../purchase-intents/schemas/purchase-intent.schema';
import { StockTransfer, StockTransferSchema } from '../stock-transfers/schemas/stock-transfer.schema';
import { Store, StoreSchema } from '../stores/schemas/store.schema';
import { MyStoreController } from './my-store.controller';
import { MyStoreInventoryExportService } from './my-store-inventory-export.service';
import { MyStoreService } from './my-store.service';

@Module({
  imports: [
    InventoryModule,
    ProductsModule,
    MongooseModule.forFeature([
      { name: Store.name, schema: StoreSchema },
      { name: PurchaseIntent.name, schema: PurchaseIntentSchema },
      { name: StockTransfer.name, schema: StockTransferSchema },
      { name: Product.name, schema: ProductSchema },
    ]),
  ],
  controllers: [MyStoreController],
  providers: [MyStoreService, MyStoreInventoryExportService],
})
export class MyStoreModule {}
