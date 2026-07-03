import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { GoodsReceipt, GoodsReceiptSchema } from '../goods-receipts/schemas/goods-receipt.schema';
import { InventoryModule } from '../inventory/inventory.module';
import { Product, ProductSchema } from '../products/schemas/product.schema';
import { LocationsModule } from '../locations/locations.module';
import { PurchaseIntentsModule } from '../purchase-intents/purchase-intents.module';
import { StoresModule } from '../stores/stores.module';
import { StockTransfer, StockTransferSchema } from './schemas/stock-transfer.schema';
import { StockTransfersController } from './stock-transfers.controller';
import { StockTransfersService } from './stock-transfers.service';

@Module({
  imports: [
    MongooseModule.forFeature([
      { name: StockTransfer.name, schema: StockTransferSchema },
      { name: Product.name, schema: ProductSchema },
      { name: GoodsReceipt.name, schema: GoodsReceiptSchema },
    ]),
    PurchaseIntentsModule,
    InventoryModule,
    StoresModule,
    LocationsModule,
  ],
  controllers: [StockTransfersController],
  providers: [StockTransfersService],
  exports: [StockTransfersService],
})
export class StockTransfersModule {}
