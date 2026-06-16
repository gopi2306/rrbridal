import type { StoreSalesDashboardPeriod, StoreSalesDashboardStore, StoreSalesPeriodPreset } from './store-sales-dashboard.types';
import type {
  StoreVendorsSalesDashboardSummary,
  StoreVendorsSalesInvoiceRow,
  StoreVendorsSalesReturnRow,
  StoreVendorsSalesVendorRow,
} from './store-vendors-sales-dashboard.types';

export interface StoreVendorsSalesReportHeaderRow {
  label: string;
  value: string;
}

export interface StoreVendorsSalesReportResponse {
  store: StoreSalesDashboardStore;
  period: StoreSalesDashboardPeriod;
  headerRows: StoreVendorsSalesReportHeaderRow[];
  summary: StoreVendorsSalesDashboardSummary;
  rows: StoreVendorsSalesVendorRow[];
  recentInvoices: StoreVendorsSalesInvoiceRow[];
  returns: StoreVendorsSalesReturnRow[];
}

export interface StoreVendorsSalesReportOptions {
  storeId?: string;
  period: StoreSalesPeriodPreset;
  from?: string;
  to?: string;
  year: number;
  month: number;
  invoiceLimit: number;
  returnDetailLimit: number;
}

export interface StoreVendorsSalesReportExportResult {
  buffer: Buffer;
  contentType: string;
  filename: string;
}

export interface StoreVendorsSalesReportFileResponse {
  report: StoreVendorsSalesReportResponse;
  file: {
    filename: string;
    contentType: string;
    base64: string;
  };
}
