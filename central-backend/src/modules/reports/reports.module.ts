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
import { CustomerBillingReportController } from './customer-billing-report.controller';
import { CustomerBillingReportExportService } from './customer-billing-report-export.service';
import { CustomerBillingReportService } from './customer-billing-report.service';
import { FastSellersReportController } from './fast-sellers-report.controller';
import { FastSellersReportExportService } from './fast-sellers-report-export.service';
import { FastSellersReportService } from './fast-sellers-report.service';
import { GoingOutOfStockReportController } from './going-out-of-stock-report.controller';
import { GoingOutOfStockReportExportService } from './going-out-of-stock-report-export.service';
import { GoingOutOfStockReportService } from './going-out-of-stock-report.service';
import { SkuSalesReportLoader } from './sku-sales-report.loader';
import { SupplierWiseSalesReportController } from './supplier-wise-sales-report.controller';
import { SupplierWiseSalesReportExportService } from './supplier-wise-sales-report-export.service';
import { SupplierWiseSalesReportService } from './supplier-wise-sales-report.service';

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
    CustomerBillingReportController,
    SupplierWiseSalesReportController,
    FastSellersReportController,
    GoingOutOfStockReportController,
    PurchaseReturnReportController,
    SalesReturnReportController,
  ],
  providers: [
    ItemDetailsReportService,
    ItemDetailsReportExportService,
    BillSummaryService,
    BillSummaryExportService,
    CustomerBillingReportService,
    CustomerBillingReportExportService,
    SkuSalesReportLoader,
    SupplierWiseSalesReportService,
    SupplierWiseSalesReportExportService,
    FastSellersReportService,
    FastSellersReportExportService,
    GoingOutOfStockReportService,
    GoingOutOfStockReportExportService,
    GstReportService,
    GstReportExportService,
    PurchaseReturnReportService,
    PurchaseReturnReportExportService,
    SalesReturnReportService,
    SalesReturnReportExportService,
  ],
})
export class ReportsModule {}
