export type WarehouseActivityKind = 'grn' | 'transfer' | 'purchase_order' | 'alert';
export type WarehouseActivityStatus = 'COMPLETED' | 'PENDING' | 'OPEN' | 'ALERT';
export type LowStockStatus = 'critical' | 'low';

export interface WarehouseDashboardWarehouse {
  code: string;
  name: string;
  subtitle: string;
}

export interface WarehouseDashboardMetrics {
  totalSkus: number;
  stockUnits: number;
  stockValue: number;
  inTransitUnits: number;
  lowStockSkus: number;
  pendingActions: number;
}

export interface WarehouseStockByCategory {
  categoryId: string;
  categoryName: string;
  pieces: number;
  percent: number;
}

export interface WarehouseRecentActivity {
  id: string;
  kind: WarehouseActivityKind;
  title: string;
  description: string;
  occurredAt: string;
  status: WarehouseActivityStatus;
}

export interface WarehouseLowStockRow {
  sku: string;
  productName: string;
  quantity: number;
  status: LowStockStatus;
}

export interface WarehouseInboundPipelineRow {
  poId: string;
  poNo: string;
  supplierName: string;
  totalPieces: number;
  expectedDate: string | null;
}

export interface WarehouseDashboardResponse {
  warehouse: WarehouseDashboardWarehouse;
  metrics: WarehouseDashboardMetrics;
  stockByCategory: WarehouseStockByCategory[];
  recentActivity: WarehouseRecentActivity[];
  lowStock: WarehouseLowStockRow[];
  inboundPipeline: WarehouseInboundPipelineRow[];
}

export interface WarehouseDashboardOptions {
  locationCode?: string;
  lowStockLimit: number;
  activityLimit: number;
  inboundDays: number;
}
