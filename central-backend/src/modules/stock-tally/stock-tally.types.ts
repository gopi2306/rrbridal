import type { StockTallyStatus } from './schemas/stock-tally.schema';

export interface StockTallyLineRow {
  sku: string;
  productName: string;
  productSubtitle: string;
  barcode: string | null;
  /** Store on-hand from inventory ledger (same as stock audit orderedQty). */
  orderedQty: number;
  /** Alias for orderedQty — store inventory quantity. */
  storeQty: number;
  scannedQty: number;
  qty: number;
  gstPercent: number | null;
  costPrice: number | null;
  mrp: number | null;
  sellingPrice: number | null;
  storePrice: number | null;
}

export interface StockTallySessionResponse {
  storeCode: string;
  tallyId: string;
  tallyNo: string;
  status: StockTallyStatus;
  skuCount: number;
  totalScannedQty: number;
  totalQty: number;
  data: StockTallyLineRow[];
  total: number;
  page: number;
  limit: number;
  totalPages: number;
}

export interface StockTallyListParams {
  page: number;
  limit: number;
  search?: string;
}

export interface StockTallySaveResponse {
  storeCode: string;
  tallyId: string;
  tallyNo: string;
  auditId: string;
  auditNo: string;
  linesSaved: number;
  savedAt: string;
  lines: Array<{ sku: string; scannedQty: number; qty: number }>;
}
