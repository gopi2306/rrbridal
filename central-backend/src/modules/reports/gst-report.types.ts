export type GstBucketRow = {
  hsn: string;
  gstPercent: number;
  qty: number;
  taxableAmount: number;
  taxAmount: number;
  totalInclusive: number;
};

export type GstRateSummaryRow = {
  gstPercent: number;
  taxableAmount: number;
  taxAmount: number;
  totalInclusive: number;
};

export type GstSectionSummary = {
  taxableAmount: number;
  taxAmount: number;
  totalInclusive: number;
  documentCount: number;
};

export type GstReportSection = {
  summary: GstSectionSummary;
  byGstRate: GstRateSummaryRow[];
  byHsn: GstBucketRow[];
};

export type GstReportResult = {
  period: { from: string; to: string; storeId?: string };
  sales: GstReportSection;
  purchase: GstReportSection;
};
