import { formatMoneyOrEmpty } from '../../../common/money.util';
import type { MyStoreInventoryGridRow } from '../my-store.types';

export const MY_STORE_INVENTORY_EXPORT_HEADERS = [
  'SKU',
  'Product',
  'Style',
  'Barcode',
  'Store qty',
  'In transit',
  'MRP',
  'Store price',
] as const;

export function storeInventoryRowsToMatrix(rows: MyStoreInventoryGridRow[]): string[][] {
  return rows.map((row) => [
    row.sku,
    row.productName,
    row.productSubtitle,
    row.barcode ?? '',
    String(row.storeQty),
    String(row.inTransitQty),
    formatMoneyOrEmpty(row.mrp),
    formatMoneyOrEmpty(row.storePrice),
  ]);
}
