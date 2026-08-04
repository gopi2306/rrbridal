import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { DocumentNumbersModule } from '../document-numbers/document-numbers.module';
import { GoodsReceipt, GoodsReceiptSchema } from '../goods-receipts/schemas/goods-receipt.schema';
import { GoodsReceiptsModule } from '../goods-receipts/goods-receipts.module';
import { Product, ProductSchema } from '../products/schemas/product.schema';
import { SuppliersModule } from '../suppliers/suppliers.module';
import { PurchaseOrderImportController } from './import/purchase-order-import.controller';
import { PurchaseOrderImportService } from './import/purchase-order-import.service';
import { PurchaseOrder, PurchaseOrderSchema } from './schemas/purchase-order.schema';
import { PurchaseOrdersController } from './purchase-orders.controller';
import { PurchaseOrdersService } from './purchase-orders.service';

@Module({
  imports: [
    DocumentNumbersModule,
    GoodsReceiptsModule,
    SuppliersModule,
    MongooseModule.forFeature([
      { name: PurchaseOrder.name, schema: PurchaseOrderSchema },
      { name: Product.name, schema: ProductSchema },
      { name: GoodsReceipt.name, schema: GoodsReceiptSchema },
    ]),
  ],
  controllers: [PurchaseOrdersController, PurchaseOrderImportController],
  providers: [PurchaseOrdersService, PurchaseOrderImportService],
  exports: [PurchaseOrdersService],
})
export class PurchaseOrdersModule {}
