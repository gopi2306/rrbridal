export interface SupplierImportRowError {
  row: number;
  name?: string;
  message: string;
}

export interface SupplierImportResult {
  totalRows: number;
  created: number;
  updated: number;
  failed: number;
  errors: SupplierImportRowError[];
  dryRun: boolean;
}

export interface SupplierImportOptions {
  dryRun?: boolean;
}

export interface ParsedSupplierImportRow {
  rowNumber: number;
  name?: string;
  gstNumber?: string;
  gstStateCode?: string;
  gstRegistrationType?: string;
  panNumber?: string;
  businessRelatedType?: string;
  contactPerson?: string;
  contactDescription?: string;
  mobileNo?: string;
  emailId?: string;
  faxNo?: string;
  offPhoneNo?: string;
  buildingAddress?: string;
  streetAddress?: string;
  landmark?: string;
  country?: string;
  state?: string;
  city?: string;
  pin?: string;
  isActive?: boolean;
  isSupplier?: boolean;
}
