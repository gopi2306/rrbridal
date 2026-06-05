import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { DocumentNumbersModule } from '../document-numbers/document-numbers.module';
import { GoodsReceipt, GoodsReceiptSchema } from '../goods-receipts/schemas/goods-receipt.schema';
import { Product, ProductSchema } from '../products/schemas/product.schema';
import { PurchaseOrder, PurchaseOrderSchema } from './schemas/purchase-order.schema';
import { PurchaseOrdersController } from './purchase-orders.controller';
import { PurchaseOrdersService } from './purchase-orders.service';

@Module({
  imports: [
    DocumentNumbersModule,
    MongooseModule.forFeature([
      { name: PurchaseOrder.name, schema: PurchaseOrderSchema },
      { name: Product.name, schema: ProductSchema },
      { name: GoodsReceipt.name, schema: GoodsReceiptSchema },
    ]),
  ],
  controllers: [PurchaseOrdersController],
  providers: [PurchaseOrdersService],
  exports: [PurchaseOrdersService],
})
export class PurchaseOrdersModule {}

