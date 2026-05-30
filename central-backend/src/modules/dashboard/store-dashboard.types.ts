export type StoreActivityKind = 'transfer' | 'purchase_intent' | 'alert';
export type StoreActivityStatus = 'COMPLETED' | 'PENDING' | 'OPEN' | 'ALERT';
export type StoreLowStockStatus = 'critical' | 'low';
export type TransferScheduleStatus = 'draft' | 'in_transit' | 'awaiting_intake';

export interface StoreDashboardStore {
  code: string;
  name: string;
  subtitle: string;
}

export interface StoreDashboardStoreOption {
  code: string;
  name: string;
}

export interface StoreDashboardMetrics {
  totalSkus: number;
  onShelfUnits: number;
  retailValue: number;
  inTransitUnits: number;
  lowStockSkus: number;
  openRequests: number;
}

export interface StoreNetworkRow {
  code: string;
  name: string;
  shelfFillPercent: number;
  totalSkus: number;
  units: number;
  lowStockSkus: number;
}

export interface StoreCategoryMixRow {
  categoryId: string;
  categoryName: string;
  pieces: number;
  percent: number;
}

export interface StoreRecentActivity {
  id: string;
  kind: StoreActivityKind;
  title: string;
  description: string;
  occurredAt: string;
  status: StoreActivityStatus;
}

export interface StoreLowStockRow {
  sku: string;
  productName: string;
  quantity: number;
  status: StoreLowStockStatus;
}

export interface StoreTransferScheduleRow {
  transferId: string;
  transferNo: string;
  title: string;
  description: string;
  expectedDate: string | null;
  status: TransferScheduleStatus;
}

export interface StoreDashboardResponse {
  store: StoreDashboardStore;
  availableStores: StoreDashboardStoreOption[];
  metrics: StoreDashboardMetrics;
  storeNetwork: StoreNetworkRow[];
  categoryMix: StoreCategoryMixRow[];
  recentActivity: StoreRecentActivity[];
  lowStock: StoreLowStockRow[];
  transferSchedule: StoreTransferScheduleRow[];
}

export interface StoreDashboardOptions {
  storeId?: string;
  lowStockLimit: number;
  activityLimit: number;
  transferLimit: number;
}
