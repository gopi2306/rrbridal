import type { ParsedProductImportRow } from './product-import.types';

export const PRODUCT_IMPORT_SHEET_NAME = 'Products';

/** Canonical header row (row 1 in template). */
export const PRODUCT_IMPORT_HEADERS: readonly string[] = [
  'sku',
  'itemName',
  'shortName',
  'alias',
  'supplierName',
  'departmentName',
  'categoryName',
  'subCategoryName',
  'manufacturerName',
  'brandName',
  'colourName',
  'productStatusName',
  'hsnName',
  'gstUomName',
  'uomSubName',
  'weightSizeName',
  'weightSizeCode',
  'weightAndSizeId',
  'weightUnitName',
  'offerGroupName',
  'skuTypeName',
  'skuOrderGroupName',
  'indentTypeName',
  'batchExpiryDetailName',
  'itemPrepStatusName',
  'packedConfirmationName',
  'poQtyPolicyName',
  'sellByName',
  'batchSelectionName',
  'itemProductType',
  'gstCode',
  'gstPercent',
  'upcEanCode',
  'subUomConversion',
  'grindingCharge',
  'weightGms',
  'decimalPoint',
  'minimumShelfFit',
  'itemPerUnit',
  'costPrice',
  'marginPercent',
  'mrp',
  'sellingPrice',
  'storePrice',
  'minStock',
  'reorderLevel',
  'unit',
  'isActive',
  'itemDiscountAllowed',
  'isWeighable',
] as const;

/** Example row for downloaded template (row 2). */
export const PRODUCT_IMPORT_EXAMPLE_ROW: Record<string, string | number | boolean> = {
  sku: 'SKU-001',
  itemName: 'Bridal Red Lehenga',
  shortName: 'Red Lehenga',
  alias: '',
  supplierName: 'Sharma Textiles',
  departmentName: 'Bridal Wear',
  categoryName: 'Lehengas',
  subCategoryName: 'Bridal Lehenga',
  manufacturerName: 'Royal Textiles',
  brandName: 'Sabyasachi',
  colourName: 'Red',
  productStatusName: 'Active',
  hsnName: 'HSN 6204',
  gstUomName: 'Pieces',
  uomSubName: 'Set',
  weightSizeName: 'Heavy',
  weightSizeCode: '',
  weightAndSizeId: '',
  weightUnitName: 'Grams',
  offerGroupName: 'Festive Offer',
  skuTypeName: 'Standard SKU',
  skuOrderGroupName: 'Group A',
  indentTypeName: 'Standard Indent',
  batchExpiryDetailName: 'No Expiry',
  itemPrepStatusName: 'Ready',
  packedConfirmationName: 'Confirmed',
  poQtyPolicyName: 'Standard PO Policy',
  sellByName: 'Retail',
  batchSelectionName: '',
  itemProductType: '',
  gstCode: '',
  gstPercent: 12,
  upcEanCode: '8901234567890',
  subUomConversion: 1,
  grindingCharge: 0,
  weightGms: 2000,
  decimalPoint: 4,
  minimumShelfFit: 1,
  itemPerUnit: 1,
  costPrice: 50000,
  marginPercent: 20,
  mrp: 95000,
  sellingPrice: 85000,
  storePrice: 85000,
  minStock: 1,
  reorderLevel: 2,
  unit: 'PCS',
  isActive: true,
  itemDiscountAllowed: true,
  isWeighable: false,
};

const HEADER_ALIASES: Record<string, keyof ParsedProductImportRow> = {
  sku: 'sku',
  itemname: 'itemName',
  'item name': 'itemName',
  shortname: 'shortName',
  'short name': 'shortName',
  alias: 'alias',
  suppliername: 'supplierName',
  'supplier name': 'supplierName',
  supplier: 'supplierName',
  departmentname: 'departmentName',
  'department name': 'departmentName',
  department: 'departmentName',
  categoryname: 'categoryName',
  'category name': 'categoryName',
  category: 'categoryName',
  subcategoryname: 'subCategoryName',
  'sub category name': 'subCategoryName',
  'sub-category name': 'subCategoryName',
  subcategory: 'subCategoryName',
  manufacturername: 'manufacturerName',
  'manufacturer name': 'manufacturerName',
  manufacturer: 'manufacturerName',
  brandname: 'brandName',
  'brand name': 'brandName',
  brand: 'brandName',
  colourname: 'colourName',
  colorname: 'colourName',
  'colour name': 'colourName',
  'color name': 'colourName',
  colour: 'colourName',
  color: 'colourName',
  productstatusname: 'productStatusName',
  'product status name': 'productStatusName',
  hsnname: 'hsnName',
  'hsn name': 'hsnName',
  hsn: 'hsnName',
  gstuomname: 'gstUomName',
  'gst uom name': 'gstUomName',
  uomsubname: 'uomSubName',
  'uom sub name': 'uomSubName',
  weightsizename: 'weightSizeName',
  'weight size name': 'weightSizeName',
  'weight size': 'weightSizeName',
  'weight and size': 'weightSizeName',
  weightandsize: 'weightSizeName',
  weightsizecode: 'weightSizeCode',
  'weight size code': 'weightSizeCode',
  weightandsizeid: 'weightAndSizeId',
  'weight and size id': 'weightAndSizeId',
  weightunitname: 'weightUnitName',
  'weight unit name': 'weightUnitName',
  offergroupname: 'offerGroupName',
  'offer group name': 'offerGroupName',
  skutypename: 'skuTypeName',
  'sku type name': 'skuTypeName',
  skuordergroupname: 'skuOrderGroupName',
  'sku order group name': 'skuOrderGroupName',
  indenttypename: 'indentTypeName',
  'indent type name': 'indentTypeName',
  batchexpirydetailname: 'batchExpiryDetailName',
  'batch expiry detail name': 'batchExpiryDetailName',
  itemprepstatusname: 'itemPrepStatusName',
  'item prep status name': 'itemPrepStatusName',
  packedconfirmationname: 'packedConfirmationName',
  'packed confirmation name': 'packedConfirmationName',
  poqtypolicyname: 'poQtyPolicyName',
  'po qty policy name': 'poQtyPolicyName',
  sellbyname: 'sellByName',
  'sell by name': 'sellByName',
  batchselectionname: 'batchSelectionName',
  'batch selection name': 'batchSelectionName',
  itemproducttype: 'itemProductType',
  'item product type': 'itemProductType',
  gstcode: 'gstCode',
  'gst code': 'gstCode',
  gstpercent: 'gstPercent',
  'gst percent': 'gstPercent',
  'gst %': 'gstPercent',
  upceancode: 'upcEanCode',
  'upc ean code': 'upcEanCode',
  barcode: 'upcEanCode',
  subuomconversion: 'subUomConversion',
  grindingcharge: 'grindingCharge',
  weightgms: 'weightGms',
  decimalpoint: 'decimalPoint',
  minimumshelffit: 'minimumShelfFit',
  itemperunit: 'itemPerUnit',
  costprice: 'costPrice',
  marginpercent: 'marginPercent',
  'margin percent': 'marginPercent',
  mrp: 'mrp',
  sellingprice: 'sellingPrice',
  storeprice: 'storePrice',
  minstock: 'minStock',
  reorderlevel: 'reorderLevel',
  unit: 'unit',
  isactive: 'isActive',
  itemdiscountallowed: 'itemDiscountAllowed',
  isweighable: 'isWeighable',
};

const NUMERIC_FIELDS = new Set<keyof ParsedProductImportRow>([
  'gstPercent',
  'subUomConversion',
  'grindingCharge',
  'weightGms',
  'decimalPoint',
  'minimumShelfFit',
  'itemPerUnit',
  'costPrice',
  'marginPercent',
  'mrp',
  'sellingPrice',
  'storePrice',
  'minStock',
  'reorderLevel',
]);

const BOOLEAN_FIELDS = new Set<keyof ParsedProductImportRow>(['isActive', 'itemDiscountAllowed', 'isWeighable']);

function normalizeHeader(h: string): string {
  return h.trim().toLowerCase().replace(/[_-]+/g, ' ');
}

export function mapHeaderToField(header: string): keyof ParsedProductImportRow | undefined {
  const key = normalizeHeader(header);
  return HEADER_ALIASES[key];
}

function parseCellValue(field: keyof ParsedProductImportRow, raw: unknown): unknown {
  if (raw === undefined || raw === null || raw === '') return undefined;
  if (NUMERIC_FIELDS.has(field)) {
    const n = typeof raw === 'number' ? raw : Number(String(raw).replace(/,/g, '').trim());
    return Number.isFinite(n) ? n : undefined;
  }
  if (BOOLEAN_FIELDS.has(field)) {
    const s = String(raw).trim().toLowerCase();
    if (['true', '1', 'yes', 'y'].includes(s)) return true;
    if (['false', '0', 'no', 'n'].includes(s)) return false;
    return undefined;
  }
  return String(raw).trim() || undefined;
}

export function rowArraysToParsedRows(
  headers: string[],
  dataRows: unknown[][],
  startRowNumber = 2,
): ParsedProductImportRow[] {
  const fieldIndexes: Array<{ field: keyof ParsedProductImportRow; index: number }> = [];
  headers.forEach((h, i) => {
    const field = mapHeaderToField(h);
    if (field && field !== 'rowNumber') fieldIndexes.push({ field, index: i });
  });

  const parsed: ParsedProductImportRow[] = [];
  let rowNumber = startRowNumber;
  for (const row of dataRows) {
    if (!row || row.every((c) => c === undefined || c === null || String(c).trim() === '')) {
      rowNumber++;
      continue;
    }
    const item: ParsedProductImportRow = { rowNumber };
    for (const { field, index } of fieldIndexes) {
      const val = parseCellValue(field, row[index]);
      if (val !== undefined) (item as unknown as Record<string, unknown>)[field] = val;
    }
    parsed.push(item);
    rowNumber++;
  }
  return parsed;
}
