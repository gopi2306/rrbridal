export type CatalogPrice = {
  productId: string;
  sku: string;
  storePrice?: number | null;
  sellingPrice?: number | null;
};

export type BillRateAuditEvent = {
  entityType: 'product';
  entityId: string;
  action: 'rate_changed';
  sku: string;
  source: 'store_billing';
  storeId?: string;
  deviceId?: string;
  billNo?: string;
  posCounter?: string;
  metadata: {
    catalogRate: number;
    billedRate: number;
    billNo: string;
    qty: number;
  };
};

export function catalogRate(product: { storePrice?: unknown; sellingPrice?: unknown }): number | null {
  const store = positiveNumber(product.storePrice);
  if (store != null) return store;
  return positiveNumber(product.sellingPrice);
}

export function billRateChanges(input: {
  billNo: string;
  storeId?: string;
  deviceId?: string;
  posCounter?: string;
  lines: unknown;
  catalogBySku: Map<string, CatalogPrice>;
}): BillRateAuditEvent[] {
  if (!Array.isArray(input.lines)) return [];
  const events: BillRateAuditEvent[] = [];
  for (const raw of input.lines) {
    if (!raw || typeof raw !== 'object') continue;
    const line = raw as Record<string, unknown>;
    const sku = typeof line.sku === 'string' ? line.sku.trim() : '';
    if (!sku) continue;
    const billed = finiteNumber(line.rate);
    if (billed == null) continue;
    const catalog = input.catalogBySku.get(sku.toLowerCase());
    if (!catalog) continue;
    const expected = catalogRate(catalog);
    if (expected == null) continue;
    if (Math.abs(billed - expected) < 0.005) continue;
    const qty = finiteNumber(line.qty) ?? 0;
    const event: BillRateAuditEvent = {
      entityType: 'product',
      entityId: catalog.productId || sku,
      action: 'rate_changed',
      sku,
      source: 'store_billing',
      metadata: {
        catalogRate: expected,
        billedRate: billed,
        billNo: input.billNo,
        qty,
      },
    };
    if (input.billNo) event.billNo = input.billNo;
    if (input.posCounter) event.posCounter = input.posCounter;
    if (input.storeId) event.storeId = input.storeId;
    if (input.deviceId) event.deviceId = input.deviceId;
    events.push(event);
  }
  return events;
}

function positiveNumber(value: unknown): number | null {
  const n = finiteNumber(value);
  if (n == null || n <= 0) return null;
  return n;
}

function finiteNumber(value: unknown): number | null {
  const n = typeof value === 'number' ? value : typeof value === 'string' && value.trim() ? Number(value) : NaN;
  return Number.isFinite(n) ? n : null;
}
