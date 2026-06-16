import type { StoreSalesDashboardPeriod, StoreSalesDashboardStore, StoreSalesPeriodPreset } from './store-sales-dashboard.types';

export const UNMAPPED_VENDOR_ID = '__unmapped__';
export const UNMAPPED_VENDOR_NAME = 'No vendor mapped';

export interface StoreVendorsSalesVendorRow {
  supplierId: string;
  vendorName: string;
  costPrice: number;
  sellingPrice: number;
  salesQty: number;
  totalCostValue: number;
  totalSellingValue: number;
  margin: number;
  marginPercent: number;
}

export interface StoreVendorsSalesInvoiceRow {
  billNo: string;
  occurredAt: string;
  qty: number;
  mappedQty: number;
  unmappedQty: number;
  hasUnmapped: boolean;
  costValue: number;
  salesAmount: number;
  margin: number;
}

export interface StoreVendorsSalesReturnRow {
  returnNo: string;
  originalBillNo: string | null;
  occurredAt: string;
  qty: number;
  mappedQty: number;
  unmappedQty: number;
  hasUnmapped: boolean;
  returnValue: number;
  lineCount: number;
}

export interface StoreVendorsSalesDashboardSummary {
  vendorCount: number;
  invoices: number;
  returnsCount: number;
  salesQty: number;
  mappedSalesQty: number;
  unmappedSalesQty: number;
  totalCostValue: number;
  totalSellingValue: number;
  margin: number;
  marginPercent: number;
  returnValue: number;
}

export interface StoreVendorsSalesDashboardResponse {
  store: StoreSalesDashboardStore;
  period: StoreSalesDashboardPeriod;
  summary: StoreVendorsSalesDashboardSummary;
  vendors: StoreVendorsSalesVendorRow[];
  recentInvoices: StoreVendorsSalesInvoiceRow[];
  returns: StoreVendorsSalesReturnRow[];
}

export interface StoreVendorsSalesDashboardOptions {
  storeId?: string;
  period: StoreSalesPeriodPreset;
  from?: string;
  to?: string;
  year: number;
  month: number;
  invoiceLimit: number;
  returnDetailLimit: number;
}
