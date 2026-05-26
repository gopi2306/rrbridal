import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { DocumentNumberService } from '../../common/document-number.service';
import { InventoryModule } from '../inventory/inventory.module';
import { IdSequence, IdSequenceSchema } from '../products/schemas/id-sequence.schema';
import { Product, ProductSchema } from '../products/schemas/product.schema';
import { GoodsReceiptNumberGenerator } from './goods-receipt-number.generator';
import { GoodsReceiptsController } from './goods-receipts.controller';
import { GoodsReceiptsService } from './goods-receipts.service';
import { GoodsReceipt, GoodsReceiptSchema } from './schemas/goods-receipt.schema';

@Module({
  imports: [
    InventoryModule,
    MongooseModule.forFeature([
      { name: GoodsReceipt.name, schema: GoodsReceiptSchema },
      { name: Product.name, schema: ProductSchema },
      { name: IdSequence.name, schema: IdSequenceSchema },
    ]),
  ],
  controllers: [GoodsReceiptsController],
  providers: [GoodsReceiptsService, DocumentNumberService, GoodsReceiptNumberGenerator],
  exports: [GoodsReceiptsService],
})
export class GoodsReceiptsModule {}

