export type MyStoreStatus = 'active' | 'inactive';

export type MyStorePurchaseIntentStatus =
  | 'submitted'
  | 'under_review'
  | 'approved'
  | 'rejected'
  | 'cancelled'
  | 'fulfilled';

export type MyStoreTransferStatus =
  | 'draft'
  | 'in_transit'
  | 'awaiting_intake'
  | 'completed'
  | 'cancelled';

export interface MyStoreProfile {
  code: string;
  name: string;
  address: string | null;
  phone: string | null;
  status: MyStoreStatus;
  updatedAt: string | null;
}

export interface MyStoreInventorySummary {
  warehouseQty: number;
  storeQty: number;
  inTransitQty: number;
  retailValue: number;
}

export interface MyStorePurchaseIndent {
  id: string;
  intentNo: string;
  status: MyStorePurchaseIntentStatus;
  statusLabel: string;
  lineCount: number;
  requestedQty: number;
  summary: string;
  description: string | null;
  updatedAt: string | null;
}

export interface MyStoreTransferCard {
  id: string;
  transferNo: string;
  status: MyStoreTransferStatus;
  statusLabel: string;
  date: string | null;
  directionLabel: string;
  lineCount: number;
  totalPieces: number;
  summary: string;
  purchaseIntentNo: string | null;
  updatedAt: string | null;
}

export interface MyStoreInventoryPreviewRow {
  sku: string;
  productName: string;
  productSubtitle: string;
  barcode: string | null;
  storeQty: number;
  inTransitQty: number;
  mrp: number | null;
  storePrice: number | null;
}

export interface MyStoreWorkspaceResponse {
  store: MyStoreProfile;
  inventorySummary: MyStoreInventorySummary;
  purchaseIndents: MyStorePurchaseIndent[];
  transfersIn: MyStoreTransferCard[];
  transfersOut: MyStoreTransferCard[];
  inventoryPreview: MyStoreInventoryPreviewRow[];
}

export interface MyStoreQueryLimits {
  purchaseIndentLimit: number;
  transferInLimit: number;
  transferOutLimit: number;
  inventoryPreviewLimit: number;
}

/** Paginated store inventory grid row (inventory grid preview UI). */
export type MyStoreInventoryGridRow = MyStoreInventoryPreviewRow;

export interface MyStoreInventoryListResponse {
  storeCode: string;
  data: MyStoreInventoryGridRow[];
  total: number;
  page: number;
  limit: number;
  totalPages: number;
}

export interface MyStoreInventoryListParams {
  page: number;
  limit: number;
  search?: string;
}
