export type ParsedPhysicalInventoryRow = {
  row: number;
  sku: string;
  itemName: string;
  newQty: number;
};

export type PhysicalInventoryImportError = {
  row: number;
  sku?: string;
  message: string;
};

export type PhysicalInventoryImportPreviewLine = {
  row: number;
  sku: string;
  itemName: string;
  qtyBefore: number;
  newQty: number;
  qtyDelta: number;
  qtyAfter: number;
  unchanged: boolean;
  warning?: string;
};

export type PhysicalInventoryImportResult = {
  dryRun: boolean;
  readyToCommit: boolean;
  totalRows: number;
  adjusted: number;
  skipped: number;
  failed: number;
  errors: PhysicalInventoryImportError[];
  lines: PhysicalInventoryImportPreviewLine[];
  adjustmentId?: string;
  adjustmentNo?: string;
};
