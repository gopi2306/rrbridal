export type StoreSalesPeriodPreset = 'today' | 'week' | 'month' | 'year' | 'custom';

export interface StoreSalesDashboardStore {
  code: string;
  name: string;
}

export interface StoreSalesDashboardPeriod {
  preset: StoreSalesPeriodPreset;
  from: string;
  to: string;
  label: string;
}

export interface StoreSalesDashboardSummary {
  grossSales: number;
  netSales: number;
  invoices: number;
  avgBasket: number;
  itemsSold: number;
  returnsCount: number;
  returnValue: number;
  discountsTotal: number;
  creditNotesIssued: number;
  creditNotesIssuedAmount: number;
  creditAppliedOnBills: number;
  creditRemainingOutstanding: number;
}

export interface StoreSalesDetailRow {
  bucketKey: string;
  label: string;
  invoices: number;
  items: number;
  gross: number;
  net: number;
  returnsCount: number;
  returnValue: number;
}

export interface StoreSalesPaymentMixRow {
  mode: string;
  pieces: number;
  amount: number;
  percent: number;
}

export interface StoreSalesTopProductRow {
  sku: string;
  description: string;
  units: number;
  percent: number;
}

export interface StoreSalesReturnDetailRow {
  returnNo: string;
  kind: 'return' | 'exchange';
  originalBillNo: string | null;
  returnMode: string | null;
  reason: string | null;
  returnTotal: number;
  replacementTotal: number;
  creditBalance: number;
  lineCount: number;
  creditNoteNo: string | null;
  customerName: string | null;
  customerPhone: string | null;
  occurredAt: string;
}

export interface StoreSalesCreditNoteApplicationRow {
  billNo: string;
  amountApplied: number;
  appliedAt: string | null;
}

export interface StoreSalesCreditNoteDetailRow {
  creditNoteNo: string;
  status: 'available' | 'consumed';
  amount: number;
  remainingAmount: number;
  totalApplied: number;
  returnNo: string | null;
  originalBillNo: string | null;
  customerCode: string | null;
  customerPhone: string | null;
  customerName: string | null;
  lastAppliedBillNo: string | null;
  consumedBillNo: string | null;
  createdAt: string;
  applications: StoreSalesCreditNoteApplicationRow[];
}

export interface StoreSalesCreditNotesSection {
  summary: {
    issuedCount: number;
    issuedAmount: number;
    appliedAmount: number;
    remainingAmount: number;
    availableCount: number;
    consumedCount: number;
  };
  items: StoreSalesCreditNoteDetailRow[];
}

export interface StoreSalesDashboardResponse {
  store: StoreSalesDashboardStore;
  period: StoreSalesDashboardPeriod;
  summary: StoreSalesDashboardSummary;
  salesDetails: StoreSalesDetailRow[];
  paymentMix: StoreSalesPaymentMixRow[];
  topProducts: StoreSalesTopProductRow[];
  returns: StoreSalesReturnDetailRow[];
  creditNotes: StoreSalesCreditNotesSection;
}

export interface StoreSalesDashboardOptions {
  storeId?: string;
  period: StoreSalesPeriodPreset;
  from?: string;
  to?: string;
  year: number;
  month: number;
  topProductLimit: number;
  returnDetailLimit: number;
  creditNoteLimit: number;
}

export interface ResolvedDateRange {
  from: Date;
  to: Date;
  fromYmd: string;
  toYmd: string;
  label: string;
  bucketByMonth: boolean;
}
