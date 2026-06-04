import { formatMoneyOrEmpty } from '../../../common/money.util';
import type { MyWarehouseInventoryGridRow } from '../my-warehouse.types';

export const MY_WAREHOUSE_INVENTORY_EXPORT_HEADERS = [
  'SKU',
  'Product',
  'Style',
  'Barcode',
  'Warehouse qty',
  'In transit',
  'Cost',
  'Selling',
] as const;

export function warehouseInventoryRowsToMatrix(rows: MyWarehouseInventoryGridRow[]): string[][] {
  return rows.map((row) => [
    row.sku,
    row.productName,
    row.productSubtitle,
    row.barcode ?? '',
    String(row.warehouseQty),
    String(row.inTransitQty),
    formatMoneyOrEmpty(row.costPrice),
    formatMoneyOrEmpty(row.sellingPrice),
  ]);
}
