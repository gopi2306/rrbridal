import type { StoreSalesDashboardPeriod, StoreSalesDashboardStore, StoreSalesPeriodPreset } from './store-sales-dashboard.types';

export const LEGACY_SALESMAN_ID = '__legacy__';

export interface StoreSalesmenSalesmanRow {
  salesmanId: string;
  salesmanCode: string | null;
  salesmanName: string;
  invoices: number;
  itemsSold: number;
  totalBillAmount: number;
}

export interface StoreSalesmenInvoiceRow {
  billNo: string;
  occurredAt: string;
  salesmanCode: string | null;
  salesmanName: string | null;
  customerName: string | null;
  qty: number;
  payable: number;
}

export interface StoreSalesmenDashboardSummary {
  salesmenCount: number;
  invoices: number;
  itemsSold: number;
  totalBillAmount: number;
}

export interface StoreSalesmenDashboardResponse {
  store: StoreSalesDashboardStore;
  period: StoreSalesDashboardPeriod;
  summary: StoreSalesmenDashboardSummary;
  salesmen: StoreSalesmenSalesmanRow[];
  recentInvoices: StoreSalesmenInvoiceRow[];
}

export interface StoreSalesmanDashboardResponse {
  store: StoreSalesDashboardStore;
  period: StoreSalesDashboardPeriod;
  salesman: StoreSalesmenSalesmanRow;
  recentInvoices: StoreSalesmenInvoiceRow[];
}

export interface StoreSalesmenDashboardOptions {
  storeId?: string;
  period: StoreSalesPeriodPreset;
  from?: string;
  to?: string;
  year: number;
  month: number;
  invoiceLimit: number;
}

export interface StoreSalesmanDashboardOptions extends StoreSalesmenDashboardOptions {
  salesmanId: string;
}
