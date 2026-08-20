export type GoingOutOfStockStatus = 'critical' | 'low';

export type GoingOutOfStockProductInput = {
  sku: string;
  itemName: string;
  minimumShelfFit?: number | null | undefined;
  minStock?: number | null | undefined;
  reorderLevel?: number | null | undefined;
  supplierId?: string | null | undefined;
  supplierName?: string | null | undefined;
};

export type GoingOutOfStockRow = {
  sku: string;
  productName: string;
  storeQty: number;
  threshold: number;
  status: GoingOutOfStockStatus;
  supplierId: string;
  supplierName: string;
};

export type GoingOutOfStockTotals = {
  skuCount: number;
  criticalCount: number;
  lowCount: number;
  zeroQtyCount: number;
};

export type GoingOutOfStockReportResponse = {
  period: {
    asOf: string;
    timezone: string;
    storeCode: string;
    storeName: string;
  };
  filters: {
    search?: string;
    status?: GoingOutOfStockStatus;
  };
  limit: number;
  truncated: boolean;
  total: number;
  totals: GoingOutOfStockTotals;
  data: GoingOutOfStockRow[];
};

export function getShelfThreshold(product: GoingOutOfStockProductInput): number | undefined {
  if (typeof product.minimumShelfFit === 'number' && Number.isFinite(product.minimumShelfFit)) {
    return product.minimumShelfFit;
  }
  if (typeof product.minStock === 'number' && Number.isFinite(product.minStock)) {
    return product.minStock;
  }
  if (typeof product.reorderLevel === 'number' && Number.isFinite(product.reorderLevel)) {
    return product.reorderLevel;
  }
  return undefined;
}

export function evaluateGoingOutOfStockRow(
  product: GoingOutOfStockProductInput,
  storeQty: number,
): GoingOutOfStockRow | null {
  const threshold = getShelfThreshold(product);
  if (threshold === undefined) return null;
  if (storeQty > threshold) return null;

  const criticalLevel =
    typeof product.minStock === 'number' && Number.isFinite(product.minStock)
      ? product.minStock
      : threshold;
  const status: GoingOutOfStockStatus = storeQty <= criticalLevel ? 'critical' : 'low';
  const supplierId = product.supplierId?.trim() || '__unmapped__';
  const supplierName =
    supplierId === '__unmapped__'
      ? 'No vendor mapped'
      : product.supplierName?.trim() || 'Unknown supplier';

  return {
    sku: product.sku,
    productName: product.itemName || product.sku,
    storeQty,
    threshold,
    status,
    supplierId,
    supplierName,
  };
}

export function collectGoingOutOfStock(
  products: readonly GoingOutOfStockProductInput[],
  storeQtyBySku: ReadonlyMap<string, number>,
): GoingOutOfStockRow[] {
  const rows: GoingOutOfStockRow[] = [];
  for (const product of products) {
    const qty = storeQtyBySku.get(product.sku) ?? 0;
    const row = evaluateGoingOutOfStockRow(product, qty);
    if (row) rows.push(row);
  }
  rows.sort(
    (a, b) =>
      a.storeQty - b.storeQty ||
      a.sku.localeCompare(b.sku, undefined, { sensitivity: 'base' }),
  );
  return rows;
}

export function filterGoingOutOfStockRows(
  rows: readonly GoingOutOfStockRow[],
  options: { search?: string; status?: GoingOutOfStockStatus },
): GoingOutOfStockRow[] {
  const search = options.search?.trim().toLocaleLowerCase('en-IN') ?? '';
  const status = options.status;
  return rows.filter((row) => {
    if (status && row.status !== status) return false;
    if (!search) return true;
    return (
      row.sku.toLocaleLowerCase('en-IN').includes(search) ||
      row.productName.toLocaleLowerCase('en-IN').includes(search) ||
      row.supplierName.toLocaleLowerCase('en-IN').includes(search)
    );
  });
}

export function totalsGoingOutOfStock(rows: readonly GoingOutOfStockRow[]): GoingOutOfStockTotals {
  let criticalCount = 0;
  let lowCount = 0;
  let zeroQtyCount = 0;
  for (const row of rows) {
    if (row.status === 'critical') criticalCount += 1;
    else lowCount += 1;
    if (row.storeQty <= 0) zeroQtyCount += 1;
  }
  return {
    skuCount: rows.length,
    criticalCount,
    lowCount,
    zeroQtyCount,
  };
}
