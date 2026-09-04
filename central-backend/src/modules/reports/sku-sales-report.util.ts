import { roundMoney } from '../../common/money.util';
import {
  UNMAPPED_VENDOR_ID,
  UNMAPPED_VENDOR_NAME,
} from '../dashboard/store-vendors-sales-dashboard.types';
import {
  readLineGrossValue,
  readNumber,
  readString,
} from '../dashboard/store-sales-payload.util';
import { isPostedSalesPayload } from './customer-billing-report.util';
import { parseReturnReportLines } from './sales-return-report.util';
import type {
  SkuCatalogEntry,
  SkuSalesRow,
  SkuSalesTotals,
  SupplierWiseSupplierRow,
} from './sku-sales-report.types';
import { UNKNOWN_SUPPLIER_NAME } from './sku-sales-report.types';

export type SkuSalesInvoiceInput = {
  invoiceNo: string;
  payload?: Record<string, unknown>;
};

export type SkuSalesReturnInput = {
  returnNo: string;
  payload?: Record<string, unknown>;
};

type Acc = {
  sku: string;
  description: string;
  soldQty: number;
  soldAmount: number;
  returnQty: number;
  returnAmount: number;
};

function acc(sku: string, description: string): Acc {
  return { sku, description, soldQty: 0, soldAmount: 0, returnQty: 0, returnAmount: 0 };
}

function lineSku(row: Record<string, unknown>): string {
  return readString(row.sku) ?? readString(row.productCode) ?? 'UNKNOWN';
}

function lineDescription(row: Record<string, unknown>, sku: string): string {
  return readString(row.description) ?? sku;
}

function addSold(map: Map<string, Acc>, row: Record<string, unknown>): void {
  const qty = readNumber(row.qty);
  if (qty <= 0) return;
  const sku = lineSku(row);
  const current = map.get(sku) ?? acc(sku, lineDescription(row, sku));
  if (!current.description && lineDescription(row, sku)) current.description = lineDescription(row, sku);
  current.soldQty = roundMoney(current.soldQty + qty);
  current.soldAmount = roundMoney(current.soldAmount + readLineGrossValue(row));
  map.set(sku, current);
}

export function collectSkuSales(
  invoices: readonly SkuSalesInvoiceInput[],
  returns: readonly SkuSalesReturnInput[],
  catalog: ReadonlyMap<string, SkuCatalogEntry>,
  supplierNames: ReadonlyMap<string, string>,
): { rows: SkuSalesRow[]; totals: SkuSalesTotals } {
  const map = new Map<string, Acc>();

  for (const invoice of invoices) {
    const payload = (invoice.payload ?? {}) as Record<string, unknown>;
    if (!isPostedSalesPayload(payload)) continue;
    const lines = payload.lines;
    if (!Array.isArray(lines)) continue;
    for (const line of lines) {
      if (!line || typeof line !== 'object') continue;
      addSold(map, line as Record<string, unknown>);
    }
  }

  for (const doc of returns) {
    const payload = (doc.payload ?? {}) as Record<string, unknown>;
    if (!isPostedSalesPayload(payload)) continue;
    for (const line of parseReturnReportLines(payload)) {
      if (line.qty <= 0) continue;
      const sku = line.sku || 'UNKNOWN';
      const current = map.get(sku) ?? acc(sku, line.itemName || sku);
      if (!current.description && line.itemName) current.description = line.itemName;
      current.returnQty = roundMoney(current.returnQty + line.qty);
      current.returnAmount = roundMoney(current.returnAmount + line.returnAmount);
      map.set(sku, current);
    }
  }

  const rows: SkuSalesRow[] = [];
  for (const item of map.values()) {
    const product = catalog.get(item.sku);
    const supplierId = product?.supplierNameId || UNMAPPED_VENDOR_ID;
    const supplierName =
      supplierId === UNMAPPED_VENDOR_ID
        ? UNMAPPED_VENDOR_NAME
        : product?.supplierName || supplierNames.get(supplierId) || UNKNOWN_SUPPLIER_NAME;
    if (product?.itemName && (!item.description || item.description === item.sku)) {
      item.description = product.itemName;
    }
    rows.push({
      sku: item.sku,
      description: item.description || item.sku,
      supplierId,
      supplierName,
      soldQty: item.soldQty,
      returnQty: item.returnQty,
      netQty: roundMoney(item.soldQty - item.returnQty),
      soldAmount: item.soldAmount,
      returnAmount: item.returnAmount,
      netAmount: roundMoney(item.soldAmount - item.returnAmount),
    });
  }

  return { rows, totals: totalsFromSkuRows(rows) };
}

export function totalsFromSkuRows(rows: readonly SkuSalesRow[]): SkuSalesTotals {
  const totals: SkuSalesTotals = {
    skuCount: rows.length,
    soldQty: 0,
    returnQty: 0,
    netQty: 0,
    soldAmount: 0,
    returnAmount: 0,
    netAmount: 0,
  };
  for (const row of rows) {
    totals.soldQty = roundMoney(totals.soldQty + row.soldQty);
    totals.returnQty = roundMoney(totals.returnQty + row.returnQty);
    totals.netQty = roundMoney(totals.netQty + row.netQty);
    totals.soldAmount = roundMoney(totals.soldAmount + row.soldAmount);
    totals.returnAmount = roundMoney(totals.returnAmount + row.returnAmount);
    totals.netAmount = roundMoney(totals.netAmount + row.netAmount);
  }
  return totals;
}

export function filterSkuRowsForSupplierSearch(
  rows: readonly SkuSalesRow[],
  search: string,
): SkuSalesRow[] {
  const q = search.trim().toLocaleLowerCase('en-IN');
  if (!q) return [...rows];
  const matchingSupplierIds = new Set(
    rows
      .filter((row) => row.supplierName.toLocaleLowerCase('en-IN').includes(q))
      .map((row) => row.supplierId),
  );
  return rows.filter(
    (row) =>
      matchingSupplierIds.has(row.supplierId) ||
      row.sku.toLocaleLowerCase('en-IN').includes(q) ||
      row.description.toLocaleLowerCase('en-IN').includes(q),
  );
}

export function filterSkuRowsForFastSearch(
  rows: readonly SkuSalesRow[],
  search: string,
): SkuSalesRow[] {
  const q = search.trim().toLocaleLowerCase('en-IN');
  if (!q) return [...rows];
  return rows.filter(
    (row) =>
      row.sku.toLocaleLowerCase('en-IN').includes(q) ||
      row.description.toLocaleLowerCase('en-IN').includes(q),
  );
}

export function rankFastSellers(rows: readonly SkuSalesRow[]): SkuSalesRow[] {
  return [...rows].sort(
    (a, b) =>
      b.netQty - a.netQty ||
      b.netAmount - a.netAmount ||
      a.sku.localeCompare(b.sku),
  );
}

/** Attach store available qty and low-stock flag (qty ≤ matchQty when matchQty > 0). Missing SKUs = 0. */
export function attachStockLevels(
  rows: readonly SkuSalesRow[],
  qtyBySku: ReadonlyMap<string, number>,
  matchQty: number,
): Array<SkuSalesRow & { availableQty: number; isLowStock: boolean }> {
  const threshold = Math.max(0, matchQty);
  return rows.map((row) => {
    const availableQty = qtyBySku.get(row.sku) ?? 0;
    return {
      ...row,
      availableQty,
      isLowStock: threshold > 0 && availableQty <= threshold,
    };
  });
}

export function groupSupplierWise(rows: readonly SkuSalesRow[]): {
  data: SupplierWiseSupplierRow[];
} {
  const groups = new Map<
    string,
    {
      supplierId: string;
      supplierName: string;
      products: SkuSalesRow[];
    }
  >();

  for (const row of rows) {
    const key = row.supplierId || UNMAPPED_VENDOR_ID;
    let group = groups.get(key);
    if (!group) {
      group = { supplierId: key, supplierName: row.supplierName, products: [] };
      groups.set(key, group);
    } else if (group.supplierName === UNMAPPED_VENDOR_NAME && row.supplierName !== UNMAPPED_VENDOR_NAME) {
      group.supplierName = row.supplierName;
    }
    group.products.push(row);
  }

  const data = [...groups.values()].map((group) => {
    group.products.sort(
      (a, b) => b.netQty - a.netQty || b.netAmount - a.netAmount || a.sku.localeCompare(b.sku),
    );
    let soldQty = 0;
    let returnQty = 0;
    let netQty = 0;
    let soldAmount = 0;
    let returnAmount = 0;
    let netAmount = 0;
    for (const product of group.products) {
      soldQty = roundMoney(soldQty + product.soldQty);
      returnQty = roundMoney(returnQty + product.returnQty);
      netQty = roundMoney(netQty + product.netQty);
      soldAmount = roundMoney(soldAmount + product.soldAmount);
      returnAmount = roundMoney(returnAmount + product.returnAmount);
      netAmount = roundMoney(netAmount + product.netAmount);
    }
    return {
      supplierId: group.supplierId,
      supplierName: group.supplierName,
      productCount: group.products.length,
      soldQty,
      returnQty,
      netQty,
      soldAmount,
      returnAmount,
      netAmount,
      products: group.products,
    };
  });

  data.sort(
    (a, b) =>
      b.netQty - a.netQty ||
      b.netAmount - a.netAmount ||
      a.supplierName.localeCompare(b.supplierName),
  );
  return { data };
}
