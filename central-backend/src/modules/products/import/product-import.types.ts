export interface ProductImportRowError {
  row: number;
  sku?: string;
  message: string;
}

export interface ProductImportResult {
  totalRows: number;
  created: number;
  updated: number;
  failed: number;
  mastersCreated: Record<string, number>;
  errors: ProductImportRowError[];
  dryRun: boolean;
}

export interface ProductImportOptions {
  dryRun?: boolean;
  createMissingMasters?: boolean;
}

/** Parsed scalar + name fields from one spreadsheet row (before master resolution). */
export interface ParsedProductImportRow {
  rowNumber: number;
  itemName?: string;
  shortName?: string;
  alias?: string;
  sku?: string;
  supplierName?: string;
  departmentName?: string;
  categoryName?: string;
  subCategoryName?: string;
  manufacturerName?: string;
  brandName?: string;
  colourName?: string;
  colourTypeName?: string;
  productStatusName?: string;
  hsnName?: string;
  gstUomName?: string;
  uomSubName?: string;
  weightSizeName?: string;
  /** 24-char hex ObjectId; used when spreadsheet has id instead of weightSizeName */
  weightAndSizeId?: string;
  /** WeightSize master code (e.g. ws-001); alternative to weightSizeName */
  weightSizeCode?: string;
  weightUnitName?: string;
  offerGroupName?: string;
  skuTypeName?: string;
  skuOrderGroupName?: string;
  indentTypeName?: string;
  batchExpiryDetailName?: string;
  itemPrepStatusName?: string;
  packedConfirmationName?: string;
  poQtyPolicyName?: string;
  sellByName?: string;
  batchSelectionName?: string;
  itemProductType?: string;
  gstCode?: string;
  gstPercent?: number;
  upcEanCode?: string;
  subUomConversion?: number;
  grindingCharge?: number;
  weightGms?: number;
  decimalPoint?: number;
  minimumShelfFit?: number;
  itemPerUnit?: number;
  costPrice?: number;
  marginPercent?: number;
  mrp?: number;
  sellingPrice?: number;
  storePrice?: number;
  minStock?: number;
  reorderLevel?: number;
  unit?: string;
  isActive?: boolean;
  itemDiscountAllowed?: boolean;
  isWeighable?: boolean;
}

/** Master ObjectId refs resolved from one import row. */
export interface ResolvedProductImportRefs {
  supplierNameId?: string | undefined;
  departmentId?: string | undefined;
  categoryId?: string | undefined;
  subCategoryId?: string | undefined;
  manufacturerNameId?: string | undefined;
  brandId?: string | undefined;
  colourIds?: string[] | undefined;
  colourTypeId?: string | undefined;
  productStatusId?: string | undefined;
  hsnCodeId?: string | undefined;
  gstUomId?: string | undefined;
  uomSubId?: string | undefined;
  weightAndSizeId?: string | undefined;
  weightPerGmOrMlId?: string | undefined;
  offerGroupId?: string | undefined;
  skuTypeId?: string | undefined;
  skuOrderGroupId?: string | undefined;
  indentTypeId?: string | undefined;
  batchExpiryDetailId?: string | undefined;
  itemPrepStatusId?: string | undefined;
  packedConfirmationId?: string | undefined;
  poQtyPolicyId?: string | undefined;
  sellById?: string | undefined;
  batchSelectionId?: string | undefined;
}
