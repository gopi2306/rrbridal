import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { InventoryModule } from '../inventory/inventory.module';
import { GoodsReceiptsController } from './goods-receipts.controller';
import { GoodsReceiptsService } from './goods-receipts.service';
import { GoodsReceipt, GoodsReceiptSchema } from './schemas/goods-receipt.schema';

@Module({
  imports: [InventoryModule, MongooseModule.forFeature([{ name: GoodsReceipt.name, schema: GoodsReceiptSchema }])],
  controllers: [GoodsReceiptsController],
  providers: [GoodsReceiptsService],
  exports: [GoodsReceiptsService],
})
export class GoodsReceiptsModule {}

