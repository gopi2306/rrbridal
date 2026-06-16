export const STORE_DAY_CLOSE_EXPORT_FORMATS = ['csv', 'xlsx'] as const;
export type StoreDayCloseExportFormat = (typeof STORE_DAY_CLOSE_EXPORT_FORMATS)[number];

export type StoreDayCloseReportOptions = {
  storeId?: string;
  businessDate: string;
  posCounter?: string;
  format: StoreDayCloseExportFormat;
};

export type StoreDayCloseReportExportResult = {
  buffer: Buffer;
  contentType: string;
  filename: string;
};

export type StoreDayCloseReportSummary = {
  openingCash: number;
  cashTotal: number;
  returnCashRefundTotal: number;
  creditNoteCashoutTotal: number;
  dailyExpensesTotal: number;
  depositsTotal: number;
  withdrawalsTotal: number;
  expectedCash: number;
  actualCashCounted: number;
  cashDifference: number;
  netCashInHand: number;
  netCardInHand: number;
  netUpiInHand: number;
  actualHandInTotal: number;
  billCount: number;
  returnCount: number;
  cardTotal: number;
  upiTotal: number;
  creditNoteTotal: number;
  returnTotalAmount: number;
  creditNoteIssuedTotal: number;
};

export type StoreDayCloseReportData = {
  store: { code: string; name: string };
  businessDate: string;
  counterScope: string;
  sessionStatus: string;
  exportedAt: string;
  summary: StoreDayCloseReportSummary;
  counterRollup: Array<{
    posCounter: string;
    status: string;
    openingCash: number;
    expectedCash: number;
    actualCashCounted: number;
    cashDifference: number;
    closedBy?: string;
    closedAtLocal?: string;
  }>;
  bills: Array<Record<string, string>>;
  returns: Array<Record<string, string>>;
  adjustments: Array<Record<string, string>>;
  expenses: Array<Record<string, string>>;
  cashMovements: Array<Record<string, string>>;
  creditNoteCashouts: Array<Record<string, string>>;
  denominations: Array<Record<string, string>>;
};
