import type { StockAuditStatus } from './schemas/stock-audit.schema';

export interface StockAuditLineRow {
  sku: string;
  productName: string;
  productSubtitle: string;
  /** Store on-hand from inventory ledger for this store. */
  orderedQty: number;
  /** Alias for orderedQty — store inventory quantity. */
  storeQty: number;
  scannedQty: number;
  varianceQty: number;
  gstPercent: number | null;
  costPrice: number | null;
  mrp: number | null;
  sellingPrice: number | null;
  storePrice: number | null;
}

export interface StockAuditListResponse {
  storeCode: string;
  auditId: string;
  auditNo: string;
  status: StockAuditStatus;
  data: StockAuditLineRow[];
  total: number;
  page: number;
  limit: number;
  totalPages: number;
}

export interface StockAuditListParams {
  page: number;
  limit: number;
  search?: string;
}
