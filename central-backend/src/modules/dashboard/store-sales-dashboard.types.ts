export type StoreSalesPeriodPreset = 'today' | 'week' | 'month' | 'year' | 'custom';

export interface StoreSalesDashboardStore {
  code: string;
  name: string;
}

export interface StoreSalesDashboardPeriod {
  preset: StoreSalesPeriodPreset;
  /** IST calendar date (YYYY-MM-DD). */
  from: string;
  /** IST calendar date (YYYY-MM-DD). */
  to: string;
  label: string;
  /** IANA timezone used to interpret from/to (Mongo compares UTC instants). */
  timezone: string;
}

export interface StoreSalesDashboardSummary {
  /** totalBillAmount + discountsTotal + creditAppliedOnBills */
  grossSales: number;
  /** Sum of bill payable amounts in the period */
  totalBillAmount: number;
  /** totalBillAmount − creditAppliedOnBills */
  netSales: number;
  /** Bill cash collected − cash refunds on returns */
  cashInHand: number;
  /** Sum of card payments on bills in the period */
  cardTotalAmount: number;
  /** Sum of UPI payments on bills in the period */
  upiTotalAmount: number;
  /** Cash refunded on sale returns (returnMode cash_refund) */
  returnCashRefundTotal: number;
  /** Cash paid out from credit note remaining balance */
  creditNoteCashoutTotal: number;
  /** returnCashRefundTotal + creditNoteCashoutTotal */
  cashRefundForReturns: number;
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
  /** Sum of cost price × qty sold (net of returns/exchanges) */
  totalCostValue: number;
  /** Sum of final line selling amounts after discounts (net of returns/exchanges) */
  totalSellingValue: number;
  /** totalSellingValue − totalCostValue */
  salesMargin: number;
  /** (salesMargin / totalCostValue) × 100 when cost > 0 */
  marginPercentage: number;
}

export interface StoreSalesBillRow {
  billNo: string;
  customerName: string | null;
  customerPhone: string | null;
  posCounter: string | null;
  payable: number;
  grossAmount: number;
  itemDiscount: number;
  cashDiscount: number;
  creditApplied: number;
  cashPaid: number;
  cardPaid: number;
  upiPaid: number;
  creditNotePaid: number;
  totalCostValue: number;
  totalSellingValue: number;
  salesMargin: number;
  marginPercentage: number;
  occurredAt: string;
}

export interface StoreSalesBillsPage {
  data: StoreSalesBillRow[];
  total: number;
  page: number;
  limit: number;
  totalPages: number;
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
  cashRefunded: number;
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
  bills: StoreSalesBillsPage;
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
  billPage: number;
  billLimit: number;
}

export interface ResolvedDateRange {
  /** UTC instant matching IST start of `fromYmd`. */
  from: Date;
  /** UTC instant matching IST end of `toYmd`. */
  to: Date;
  /** IST calendar date (YYYY-MM-DD). */
  fromYmd: string;
  /** IST calendar date (YYYY-MM-DD). */
  toYmd: string;
  label: string;
  bucketByMonth: boolean;
  timezone: string;
}
