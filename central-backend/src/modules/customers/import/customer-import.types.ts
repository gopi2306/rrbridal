export interface CustomerImportRowError {
  row: number;
  customerCode?: string;
  message: string;
}

export interface CustomerImportResult {
  totalRows: number;
  created: number;
  updated: number;
  failed: number;
  errors: CustomerImportRowError[];
  dryRun: boolean;
}

export interface CustomerImportOptions {
  dryRun?: boolean;
}

export interface ParsedCustomerImportRow {
  rowNumber: number;
  customerCode?: string;
  name?: string;
  phone?: string;
  email?: string;
  gstin?: string;
  addressLine1?: string;
  addressLine2?: string;
  city?: string;
  state?: string;
  pincode?: string;
  isActive?: boolean;
}
