/**
 * Self-test: bill line rate vs catalog rate.
 * Run: npx ts-node -r reflect-metadata src/modules/audit-logs/bill-rate-audit.selftest.ts
 */
import { billRateChanges, catalogRate, type CatalogPrice } from './bill-rate-audit.util';

function assert(cond: unknown, msg: string): asserts cond {
  if (!cond) throw new Error(msg);
}

assert(catalogRate({ storePrice: 0, sellingPrice: 120 }) === 120, 'zero store price falls back to selling price');
assert(catalogRate({ storePrice: 90, sellingPrice: 120 }) === 90, 'positive store price wins');

const catalog = new Map<string, CatalogPrice>([
  ['sku-a', { productId: 'p1', sku: 'SKU-A', storePrice: 100, sellingPrice: 80 }],
  ['sku-b', { productId: 'p2', sku: 'SKU-B', sellingPrice: 50 }],
]);

const events = billRateChanges({
  billNo: 'B-1',
  storeId: 'ST',
  posCounter: 'C3',
  lines: [
    { sku: 'SKU-A', rate: 80, qty: 2 },
    { sku: 'SKU-B', rate: 50, qty: 1 },
  ],
  catalogBySku: catalog,
});

assert(events.length === 1, 'only the changed rate is logged');
const event = events[0];
assert(event, 'event exists');
assert(event.action === 'rate_changed', 'action');
assert(event.sku === 'SKU-A', 'sku');
assert(event.entityId === 'p1', 'product id');
assert(event.metadata.catalogRate === 100 && event.metadata.billedRate === 80, 'rates');
assert(event.metadata.billNo === 'B-1' && event.metadata.qty === 2, 'bill context');
assert(event.source === 'store_billing', 'source');
assert(event.billNo === 'B-1' && event.posCounter === 'C3', 'bill and pos stored');

console.log('bill-rate-audit.selftest ok');
