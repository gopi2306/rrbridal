import type { StoreSalesDashboardPeriod, StoreSalesPeriodPreset } from './store-sales-dashboard.types';

export type StoreBillMarginOptions = {
  storeId?: string;
  period: StoreSalesPeriodPreset;
  from?: string;
  to?: string;
  year: number;
  month: number;
  salesmanId?: string;
  posCounter?: string;
  /** Max bill rows to return (default 5000, hard cap 5000). */
  limit?: number;
};

export type StoreBillMarginStore = {
  code: string;
  name: string;
};

export type StoreBillMarginSummary = {
  billCount: number;
  totalQty: number;
  totalCost: number;
  totalSelling: number;
  totalDiscount: number;
  totalMargin: number;
  marginPercentage: number;
};

export type StoreBillMarginRow = {
  billNo: string;
  postedAt: string;
  billDate: string;
  posCounter: string | null;
  customerName: string | null;
  salesmanCode: string | null;
  salesmanName: string | null;
  qty: number;
  costPrice: number;
  sellingPrice: number;
  discount: number;
  marginAmount: number;
  marginPercentage: number;
  hasReturn: boolean;
  returnNo: string | null;
  hasAdjustment: boolean;
  adjustmentNo: string | null;
};

export type StoreBillMarginResponse = {
  store: StoreBillMarginStore;
  period: StoreSalesDashboardPeriod;
  summary: StoreBillMarginSummary;
  rows: StoreBillMarginRow[];
  totalMatched: number;
  wasTruncated: boolean;
};

export type StoreBillMarginExportResult = {
  filename: string;
  contentType: string;
  buffer: Buffer;
};
