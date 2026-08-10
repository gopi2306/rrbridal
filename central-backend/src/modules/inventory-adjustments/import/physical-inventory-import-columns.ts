import { ParsedPhysicalInventoryRow, PhysicalInventoryImportError } from './physical-inventory-import.types';

export const PHYSICAL_INVENTORY_HEADERS = ['SKU', 'Item Name', 'Phy Qty'] as const;
export const PHYSICAL_INVENTORY_SHEET = 'Physical Inventory';
export const PHYSICAL_INVENTORY_MAX_ROWS = 10_000;

const HEADER_ALIASES: Record<string, keyof HeaderIndexes> = {
  sku: 'sku',
  itemname: 'itemName',
  productname: 'itemName',
  phyqty: 'newQty',
  physicalqty: 'newQty',
  physicalquantity: 'newQty',
  newqty: 'newQty',
};

type HeaderIndexes = {
  sku: number;
  itemName: number;
  newQty: number;
};

function normalizeHeader(value: unknown): string {
  return String(value ?? '')
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]/g, '');
}

export function parsePhysicalInventoryRows(
  headers: unknown[],
  dataRows: unknown[][],
  firstRowNumber = 2,
): { rows: ParsedPhysicalInventoryRow[]; errors: PhysicalInventoryImportError[]; sourceRows: number } {
  const indexes: Partial<HeaderIndexes> = {};
  headers.forEach((header, index) => {
    const field = HEADER_ALIASES[normalizeHeader(header)];
    if (field !== undefined && indexes[field] === undefined) indexes[field] = index;
  });
  if (indexes.sku === undefined || indexes.newQty === undefined) {
    throw new Error("Excel headers must include 'SKU' and 'Phy Qty'");
  }

  const rows: ParsedPhysicalInventoryRow[] = [];
  const errors: PhysicalInventoryImportError[] = [];
  const seen = new Map<string, number>();
  let sourceRows = 0;

  dataRows.forEach((cells, offset) => {
    const row = firstRowNumber + offset;
    const sku = String(cells[indexes.sku!] ?? '').trim();
    const qtyRaw = cells[indexes.newQty!];
    const itemName =
      indexes.itemName === undefined ? '' : String(cells[indexes.itemName] ?? '').trim();
    const qtyText = String(qtyRaw ?? '').trim();
    if (!qtyText) return;
    sourceRows++;
    if (!sku) {
      errors.push({ row, message: 'SKU is required' });
      return;
    }
    const newQty = Number(qtyRaw);
    if (!Number.isFinite(newQty) || newQty < 0) {
      errors.push({ row, sku, message: 'Phy Qty must be a number zero or greater' });
      return;
    }
    if (seen.has(sku)) {
      errors.push({ row, sku, message: `Duplicate SKU; first used on row ${seen.get(sku)}` });
      return;
    }
    seen.set(sku, row);
    rows.push({ row, sku, itemName, newQty });
  });

  return { rows, errors, sourceRows };
}
