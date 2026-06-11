import { formatMoneyOrEmpty } from '../../../common/money.util';
import type { StockAuditLineRow } from '../stock-audit.types';

export const STOCK_AUDIT_EXPORT_HEADERS = [
  'SKU',
  'Product',
  'Style',
  'Ordered qty',
  'Scanned qty',
  'Variance',
  'GST %',
  'Cost price',
  'MRP',
  'Selling',
  'Store price',
] as const;

export function stockAuditRowsToMatrix(rows: StockAuditLineRow[]): string[][] {
  return rows.map((row) => [
    row.sku,
    row.productName,
    row.productSubtitle,
    String(row.orderedQty),
    String(row.scannedQty),
    String(row.varianceQty),
    row.gstPercent != null ? String(row.gstPercent) : '',
    formatMoneyOrEmpty(row.costPrice),
    formatMoneyOrEmpty(row.mrp),
    formatMoneyOrEmpty(row.sellingPrice),
    formatMoneyOrEmpty(row.storePrice),
  ]);
}
