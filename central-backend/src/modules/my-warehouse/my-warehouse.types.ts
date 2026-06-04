export type MyWarehouseGoodsReceiptStatus = 'draft' | 'posted';

export type MyWarehousePurchaseOrderStatus =
  | 'open'
  | 'awaiting_approval'
  | 'approved'
  | 'partially_received'
  | 'received'
  | 'closed';

export type MyWarehouseTransferStatus =
  | 'draft'
  | 'in_transit'
  | 'awaiting_intake'
  | 'completed'
  | 'cancelled';

export interface MyWarehouseProfile {
  code: string;
  name: string;
  address: string | null;
  phone: string | null;
  type: string;
  updatedAt: string | null;
}

export interface MyWarehouseInventorySummary {
  warehouseQty: number;
  inTransitQty: number;
  stockValue: number;
}

export interface MyWarehouseGoodsReceipt {
  id: string;
  receiptNo: string;
  grnNumber: string | null;
  mrcNumber: string | null;
  poNo: string | null;
  supplierName: string | null;
  reference: string | null;
  lineCount: number;
  validQty: number;
  damagedCount: number;
  summary: string;
  status: MyWarehouseGoodsReceiptStatus;
  statusLabel: string;
  updatedAt: string | null;
}

export interface MyWarehousePurchaseOrder {
  id: string;
  poNo: string;
  supplierName: string;
  deliveryDate: string | null;
  totalPieces: number;
  summary: string;
  status: MyWarehousePurchaseOrderStatus;
  statusLabel: string;
  updatedAt: string | null;
}

export interface MyWarehouseTransferOut {
  id: string;
  transferNo: string;
  status: MyWarehouseTransferStatus;
  statusLabel: string;
  date: string | null;
  classificationTag: string | null;
  toStoreId: string | null;
  toStoreName: string | null;
  purchaseIntentNo: string | null;
  lineCount: number;
  totalPieces: number;
  updatedAt: string | null;
}

export interface MyWarehouseInventoryPreviewRow {
  sku: string;
  productName: string;
  productSubtitle: string;
  barcode: string | null;
  warehouseQty: number;
  inTransitQty: number;
  costPrice: number | null;
  sellingPrice: number | null;
}

export interface MyWarehouseWorkspaceResponse {
  warehouse: MyWarehouseProfile;
  inventorySummary: MyWarehouseInventorySummary;
  goodsReceipts: MyWarehouseGoodsReceipt[];
  purchaseOrders: MyWarehousePurchaseOrder[];
  transfersOut: MyWarehouseTransferOut[];
  inventoryPreview: MyWarehouseInventoryPreviewRow[];
}

export interface MyWarehouseQueryLimits {
  goodsReceiptLimit: number;
  purchaseOrderLimit: number;
  transferOutLimit: number;
  inventoryPreviewLimit: number;
}

/** Paginated warehouse inventory grid row (inventory grid preview UI). */
export type MyWarehouseInventoryGridRow = MyWarehouseInventoryPreviewRow;

export interface MyWarehouseInventoryListResponse {
  locationCode: string;
  data: MyWarehouseInventoryGridRow[];
  total: number;
  page: number;
  limit: number;
  totalPages: number;
}

export interface MyWarehouseInventoryListParams {
  page: number;
  limit: number;
  search?: string;
}
