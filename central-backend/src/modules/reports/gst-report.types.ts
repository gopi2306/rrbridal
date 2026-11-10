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

/** B2B when the buyer GSTIN is on the bill, otherwise B2C. */
export type GstSupplyCategory = 'B2B' | 'B2C';

export type GstSalesInvoiceRow = GstTaxBreakdown & {
  storeId: string;
  documentType: 'invoice' | 'return' | 'exchange';
  documentNo: string;
  documentDate: string;
  customerCode?: string;
  customerName?: string;
  customerPhone?: string;
  customerGstin?: string;
  supplyCategory: GstSupplyCategory;
  isInterState?: boolean;
  lineCount: number;
};

/** One row per buyer: grouped by GSTIN, else phone, else code, else walk-in. */
export type GstSalesCustomerRow = GstTaxBreakdown & {
  customerKey: string;
  customerCode?: string;
  customerName?: string;
  customerPhone?: string;
  customerGstin?: string;
  supplyCategory: GstSupplyCategory;
  invoiceCount: number;
  returnCount: number;
  firstDocumentDate: string;
  lastDocumentDate: string;
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
  byCustomer: GstSalesCustomerRow[];
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
