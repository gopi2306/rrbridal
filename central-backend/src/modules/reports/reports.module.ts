import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { GoodsReceipt, GoodsReceiptSchema } from '../goods-receipts/schemas/goods-receipt.schema';
import { HsnCode, HsnCodeSchema } from '../hsn-codes/schemas/hsn-code.schema';
import { InventoryModule } from '../inventory/inventory.module';
import { Store, StoreSchema } from '../stores/schemas/store.schema';
import {
  PurchaseOrder,
  PurchaseOrderSchema,
} from '../purchase-orders/schemas/purchase-order.schema';
import { Product, ProductSchema } from '../products/schemas/product.schema';
import { Supplier, SupplierSchema } from '../suppliers/schemas/supplier.schema';
import { StoreInvoice, StoreInvoiceSchema } from '../store-sales/schemas/store-invoice.schema';
import {
  StoreSaleReturn,
  StoreSaleReturnSchema,
} from '../store-sales/schemas/store-sale-return.schema';
import { GstReportController } from './gst-report.controller';
import { GstReportExportService } from './gst-report-export.service';
import { GstReportService } from './gst-report.service';
import { ItemDetailsReportController } from './item-details-report.controller';
import { ItemDetailsReportExportService } from './item-details-report-export.service';
import { ItemDetailsReportService } from './item-details-report.service';
import { BillSummaryReportController } from './bill-summary.controller';
import { BillSummaryService } from './bill-summary.service';
import { BillSummaryExportService } from './bill-summary-export.service';
import { PurchaseReturnReportController } from './purchase-return-report.controller';
import { PurchaseReturnReportExportService } from './purchase-return-report-export.service';
import { PurchaseReturnReportService } from './purchase-return-report.service';
import { SalesReturnReportController } from './sales-return-report.controller';
import { SalesReturnReportExportService } from './sales-return-report-export.service';
import { SalesReturnReportService } from './sales-return-report.service';
import { Branch, BranchSchema } from '../branches/schemas/branch.schema';
import { Division, DivisionSchema } from '../divisions/schemas/division.schema';
import { Location, LocationSchema } from '../locations/schemas/location.schema';
import {
  PurchaseReturn,
  PurchaseReturnSchema,
} from '../purchase-returns/schemas/purchase-return.schema';
import {
  CompanyProfile,
  CompanyProfileSchema,
} from '../company-profile/schemas/company-profile.schema';

@Module({
  imports: [
    InventoryModule,
    MongooseModule.forFeature([
      { name: PurchaseOrder.name, schema: PurchaseOrderSchema },
      { name: GoodsReceipt.name, schema: GoodsReceiptSchema },
      { name: StoreInvoice.name, schema: StoreInvoiceSchema },
      { name: StoreSaleReturn.name, schema: StoreSaleReturnSchema },
      { name: Product.name, schema: ProductSchema },
      { name: HsnCode.name, schema: HsnCodeSchema },
      { name: Supplier.name, schema: SupplierSchema },
      { name: Store.name, schema: StoreSchema },
      { name: Branch.name, schema: BranchSchema },
      { name: Division.name, schema: DivisionSchema },
      { name: Location.name, schema: LocationSchema },
      { name: PurchaseReturn.name, schema: PurchaseReturnSchema },
      { name: CompanyProfile.name, schema: CompanyProfileSchema },
    ]),
  ],
  controllers: [
    ItemDetailsReportController,
    GstReportController,
    BillSummaryReportController,
    PurchaseReturnReportController,
    SalesReturnReportController,
  ],
  providers: [
    ItemDetailsReportService,
    ItemDetailsReportExportService,
    BillSummaryService,
    BillSummaryExportService,
    GstReportService,
    GstReportExportService,
    PurchaseReturnReportService,
    PurchaseReturnReportExportService,
    SalesReturnReportService,
    SalesReturnReportExportService,
  ],
})
export class ReportsModule {}
