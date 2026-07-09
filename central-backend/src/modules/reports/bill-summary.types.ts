export type BillSummaryGstBucket = {
  gstPercent: number;
  taxableAmount: number;
  taxAmount: number;
  sgstPercent: number;
  cgstPercent: number;
  igstPercent: number;
  sgstAmount: number;
  cgstAmount: number;
  igstAmount: number;
  totalInclusive: number;
};

export type BillSummaryRow = {
  billDate: string;
  counter: string;
  purchaseBillNo: string;
  customerName: string;
  totalQty: number;

  goodsValue: number;
  discountAmount: number;
  taxAmount: number;
  billAmount: number;

  grossMargin: number;

  cashAmount: number;
  cardAmount: number;
  creditNoteAmount: number;
  upiAmount: number;

  billNo: string;
  rrn: string;

  gstBuckets: Record<number, BillSummaryGstBucket>;
};

export type BillSummaryReportResponse = {
  period: { from: string; to: string; storeCode?: string; storeName?: string };
  truncated: boolean;
  total: number;
  gstPercents: number[];
  data: BillSummaryRow[];
};

