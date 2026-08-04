export type PurchaseOrderImportError = {
  row: number;
  sku?: string;
  message: string;
};

export type PurchaseOrderImportLine = {
  sku: string;
  qty: number;
  /** 1-based Excel row of the first occurrence (for error reporting). */
  sourceRow: number;
};

export type PurchaseOrderImportOptions = {
  dryRun?: boolean;
  supplierId: string;
  supplierName?: string;
  mainLocationId?: string;
  branchId?: string;
  mainDivisionId?: string;
};

export type PurchaseOrderImportResult = {
  dryRun: boolean;
  totalRows: number;
  lineCount: number;
  poId?: string;
  poNo?: string;
  grId?: string;
  receiptNo?: string;
  posted?: boolean;
  warnings: string[];
  errors: PurchaseOrderImportError[];
};
