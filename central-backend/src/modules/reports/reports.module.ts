import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { GoodsReceipt, GoodsReceiptSchema } from '../goods-receipts/schemas/goods-receipt.schema';
import { InventoryModule } from '../inventory/inventory.module';
import {
  PurchaseOrder,
  PurchaseOrderSchema,
} from '../purchase-orders/schemas/purchase-order.schema';
import { Product, ProductSchema } from '../products/schemas/product.schema';
import { StoreInvoice, StoreInvoiceSchema } from '../store-sales/schemas/store-invoice.schema';
import {
  StoreSaleReturn,
  StoreSaleReturnSchema,
} from '../store-sales/schemas/store-sale-return.schema';
import { ItemDetailsReportController } from './item-details-report.controller';
import { ItemDetailsReportExportService } from './item-details-report-export.service';
import { ItemDetailsReportService } from './item-details-report.service';

@Module({
  imports: [
    InventoryModule,
    MongooseModule.forFeature([
      { name: PurchaseOrder.name, schema: PurchaseOrderSchema },
      { name: GoodsReceipt.name, schema: GoodsReceiptSchema },
      { name: StoreInvoice.name, schema: StoreInvoiceSchema },
      { name: StoreSaleReturn.name, schema: StoreSaleReturnSchema },
      { name: Product.name, schema: ProductSchema },
    ]),
  ],
  controllers: [ItemDetailsReportController],
  providers: [ItemDetailsReportService, ItemDetailsReportExportService],
})
export class ReportsModule {}
