export type GstTaxBreakdown = {
  gstPercent: number;
  sgstPercent: number;
  cgstPercent: number;
  igstPercent: number;
  taxableAmount: number;
  taxAmount: number;
  sgstAmount: number;
  cgstAmount: number;
  igstAmount: number;
  totalInclusive: number;
};

export type GstSectionSummary = GstTaxBreakdown & {
  documentCount: number;
};

export type GstRateSummaryRow = GstTaxBreakdown;

export type GstHsnRow = GstTaxBreakdown & {
  hsn: string;
  qty: number;
};

export type GstItemRow = GstTaxBreakdown & {
  sku: string;
  itemName?: string;
  hsn?: string;
  qty: number;
};

export type GstSalesInvoiceRow = GstTaxBreakdown & {
  storeId: string;
  documentType: 'invoice' | 'return' | 'exchange';
  documentNo: string;
  documentDate: string;
  customerName?: string;
  isInterState?: boolean;
  lineCount: number;
};

export type GstPurchaseInvoiceRow = GstTaxBreakdown & {
  grnNumber?: string;
  receiptNo: string;
  poNo?: string;
  invoiceNo?: string;
  invoiceDate?: string;
  supplierName?: string;
  supplierGstNumber?: string;
  receivedQty: number;
  purchaseCost?: number;
  discountAmount?: number;
};

export type GstSalesReportSection = {
  summary: GstSectionSummary;
  byGstRate: GstRateSummaryRow[];
  byHsn: GstHsnRow[];
  byItem: GstItemRow[];
  byInvoice: GstSalesInvoiceRow[];
};

export type GstPurchaseReportSection = {
  summary: GstSectionSummary;
  byGstRate: GstRateSummaryRow[];
  byHsn: GstHsnRow[];
  byItem: GstItemRow[];
  byInvoice: GstPurchaseInvoiceRow[];
};

export type GstReportResult = {
  period: { from: string; to: string; storeId?: string };
  sales: GstSalesReportSection;
  purchase: GstPurchaseReportSection;
};

export type GstSalesReportResult = {
  period: { from: string; to: string; storeId?: string };
} & GstSalesReportSection;

export type GstPurchaseReportResult = {
  period: { from: string; to: string };
} & GstPurchaseReportSection;

/** @deprecated Use GstHsnRow */
export type GstBucketRow = GstHsnRow;
