import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { DocumentNumbersModule } from '../document-numbers/document-numbers.module';
import { InventoryModule } from '../inventory/inventory.module';
import { Product, ProductSchema } from '../products/schemas/product.schema';
import { GoodsReceiptNumberGenerator } from './goods-receipt-number.generator';
import { GoodsReceiptsController } from './goods-receipts.controller';
import { GoodsReceiptsService } from './goods-receipts.service';
import { GoodsReceipt, GoodsReceiptSchema } from './schemas/goods-receipt.schema';

@Module({
  imports: [
    DocumentNumbersModule,
    InventoryModule,
    MongooseModule.forFeature([
      { name: GoodsReceipt.name, schema: GoodsReceiptSchema },
      { name: Product.name, schema: ProductSchema },
    ]),
  ],
  controllers: [GoodsReceiptsController],
  providers: [GoodsReceiptsService, GoodsReceiptNumberGenerator],
  exports: [GoodsReceiptsService],
})
export class GoodsReceiptsModule {}

