import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { DocumentNumbersModule } from '../document-numbers/document-numbers.module';
import { InventoryModule } from '../inventory/inventory.module';
import { Product, ProductSchema } from '../products/schemas/product.schema';
import {
  PurchaseOrder,
  PurchaseOrderSchema,
} from '../purchase-orders/schemas/purchase-order.schema';
import { GoodsReceiptReportController } from './goods-receipt-report.controller';
import { GoodsReceiptReportService } from './goods-receipt-report.service';
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
      { name: PurchaseOrder.name, schema: PurchaseOrderSchema },
    ]),
  ],
  controllers: [GoodsReceiptReportController, GoodsReceiptsController],
  providers: [GoodsReceiptsService, GoodsReceiptReportService, GoodsReceiptNumberGenerator],
  exports: [GoodsReceiptsService],
})
export class GoodsReceiptsModule {}

