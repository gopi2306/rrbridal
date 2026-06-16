import type { StoreSalesDashboardPeriod, StoreSalesDashboardStore, StoreSalesPeriodPreset } from './store-sales-dashboard.types';

export interface StoreVendorSalesDashboardSupplier {
  id: string;
  name: string;
}

export interface StoreVendorSalesDashboardSummary {
  grossSales: number;
  netSales: number;
  returnValue: number;
  returnsCount: number;
  invoices: number;
  itemsSold: number;
  totalCostValue: number;
  totalSellingValue: number;
  salesMargin: number;
  marginPercentage: number;
}

export interface StoreVendorSalesMarginInsights {
  marginPercentage: number;
  avgSalePerUnit: number;
  avgCostPerUnit: number;
  avgInvoiceValue: number;
  returnsQty: number;
  returnsValue: number;
}

export interface StoreVendorSalesDetailRow {
  bucketKey: string;
  label: string;
  invoices: number;
  items: number;
  gross: number;
  net: number;
  returnsCount: number;
  returnValue: number;
}

export interface StoreVendorSalesReturnBreakdownRow {
  bucketKey: string;
  label: string;
  returns: number;
  returnValue: number;
}

export interface StoreVendorSalesTopProductRow {
  sku: string;
  description: string;
  units: number;
  salesAmount: number;
  margin: number;
  percent: number;
}

export interface StoreVendorSalesCategoryMixRow {
  categoryId: string;
  categoryName: string;
  pieces: number;
  percent: number;
}

export interface StoreVendorSalesInvoiceRow {
  billNo: string;
  occurredAt: string;
  qty: number;
  costValue: number;
  salesAmount: number;
  margin: number;
}

export interface StoreVendorSalesReturnRow {
  returnNo: string;
  originalBillNo: string | null;
  occurredAt: string;
  qty: number;
  returnValue: number;
  lineCount: number;
}

export interface StoreVendorSalesDashboardResponse {
  store: StoreSalesDashboardStore;
  supplier: StoreVendorSalesDashboardSupplier;
  period: StoreSalesDashboardPeriod;
  summary: StoreVendorSalesDashboardSummary;
  marginInsights: StoreVendorSalesMarginInsights;
  salesDetails: StoreVendorSalesDetailRow[];
  returnBreakdown: StoreVendorSalesReturnBreakdownRow[];
  topProducts: StoreVendorSalesTopProductRow[];
  categoryMix: StoreVendorSalesCategoryMixRow[];
  recentInvoices: StoreVendorSalesInvoiceRow[];
  returns: StoreVendorSalesReturnRow[];
}

export interface StoreVendorSalesDashboardOptions {
  storeId?: string;
  supplierId: string;
  period: StoreSalesPeriodPreset;
  from?: string;
  to?: string;
  year: number;
  month: number;
  topProductLimit: number;
  invoiceLimit: number;
  returnDetailLimit: number;
}
