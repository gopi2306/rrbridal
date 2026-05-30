import type { WarehouseStoreGridRow } from '../inventory.service';

export const INVENTORY_EXPORT_HEADERS = [
  'SKU',
  'Barcode',
  'Product',
  'Brand',
  'Category',
  'Warehouse qty',
  'In transit',
  'Store qty',
  'Cost price',
  'MRP',
  'Selling price',
  'Store price',
  'GST %',
] as const;

export type InventoryExportHeader = (typeof INVENTORY_EXPORT_HEADERS)[number];

export function readPopulatedName(ref: unknown): string {
  if (!ref || typeof ref !== 'object') return '';
  const name = (ref as Record<string, unknown>).name;
  return typeof name === 'string' ? name.trim() : '';
}

function readNumber(value: unknown): number | '' {
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  return '';
}

export function gridRowToExportCells(row: WarehouseStoreGridRow): string[] {
  const product = row.product;
  return [
    row.sku,
    row.upcEanCode ?? '',
    typeof product.itemName === 'string' ? product.itemName : '',
    readPopulatedName(product.brandId),
    readPopulatedName(product.categoryId),
    String(row.warehouseQty),
    String(row.inTransitQty),
    String(row.storeQty),
    String(readNumber(product.costPrice)),
    String(readNumber(row.mrp)),
    String(readNumber(product.sellingPrice)),
    String(readNumber(row.storePrice)),
    String(readNumber(product.gstPercent)),
  ];
}

export function gridRowsToMatrix(rows: WarehouseStoreGridRow[]): string[][] {
  return rows.map((row) => gridRowToExportCells(row));
}

export function escapeCsvCell(value: unknown): string {
  const s = value === null || value === undefined ? '' : String(value);
  if (/[",\n\r]/.test(s)) return `"${s.replace(/"/g, '""')}"`;
  return s;
}

export function matrixToCsv(headers: readonly string[], rows: string[][]): Buffer {
  const lines = [
    headers.map(escapeCsvCell).join(','),
    ...rows.map((row) => row.map(escapeCsvCell).join(',')),
  ];
  const body = lines.join('\r\n');
  return Buffer.from('\uFEFF' + body, 'utf-8');
}
