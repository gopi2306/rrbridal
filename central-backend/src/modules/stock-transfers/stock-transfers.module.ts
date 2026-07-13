import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { GoodsReceipt, GoodsReceiptSchema } from '../goods-receipts/schemas/goods-receipt.schema';
import { InventoryModule } from '../inventory/inventory.module';
import { Product, ProductSchema } from '../products/schemas/product.schema';
import { LocationsModule } from '../locations/locations.module';
import { PurchaseIntentsModule } from '../purchase-intents/purchase-intents.module';
import { StoresModule } from '../stores/stores.module';
import {
  GrnAutoTransferHistory,
  GrnAutoTransferHistorySchema,
} from './schemas/grn-auto-transfer-history.schema';
import { StockTransfer, StockTransferSchema } from './schemas/stock-transfer.schema';
import { GrnAutoTransferHistoryService } from './grn-auto-transfer-history.service';
import { StockTransfersController } from './stock-transfers.controller';
import { StockTransfersService } from './stock-transfers.service';

@Module({
  imports: [
    MongooseModule.forFeature([
      { name: StockTransfer.name, schema: StockTransferSchema },
      { name: Product.name, schema: ProductSchema },
      { name: GoodsReceipt.name, schema: GoodsReceiptSchema },
      { name: GrnAutoTransferHistory.name, schema: GrnAutoTransferHistorySchema },
    ]),
    PurchaseIntentsModule,
    InventoryModule,
    StoresModule,
    LocationsModule,
  ],
  controllers: [StockTransfersController],
  providers: [StockTransfersService, GrnAutoTransferHistoryService],
  exports: [StockTransfersService],
})
export class StockTransfersModule {}
