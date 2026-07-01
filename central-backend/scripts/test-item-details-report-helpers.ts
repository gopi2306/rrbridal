/**
 * Lightweight helper tests for item-details report parsing.
 * Run: npx ts-node -r reflect-metadata scripts/test-item-details-report-helpers.ts
 */
import assert from 'node:assert/strict';
import {
  aggregateSalesQtyBySku,
  buildItemDetailsSummary,
  enrichSohRowsWithSalesQty,
  isDateInBounds,
  paginateRows,
  parseDocumentDate,
  parseInclusiveDateBounds,
  parseSalesLinesFromPayload,
  passesProductFilters,
} from '../src/modules/reports/item-details-report.helpers';
import type { SalesItemRow, SohItemRow } from '../src/modules/reports/item-details-report.types';

function testDateBounds() {
  const bounds = parseInclusiveDateBounds('2026-01-01', '2026-01-31');
  const inside = parseDocumentDate('2026-01-15');
  const outside = parseDocumentDate('2026-02-01');
  assert.equal(isDateInBounds(inside, bounds), true);
  assert.equal(isDateInBounds(outside, bounds), false);
}

function testProductFilters() {
  const product = {
    sku: 'SKU-001',
    itemName: 'Silk Saree',
    brandName: 'PAKEEZA',
    brandKey: 'brand-001',
    supplierKey: 'sup-001',
  };
  assert.equal(passesProductFilters('SKU-001', 'Silk Saree', product, { sku: 'SKU-001' }), true);
  assert.equal(passesProductFilters('SKU-001', 'Silk Saree', product, { search: 'saree' }), true);
  assert.equal(passesProductFilters('SKU-001', 'Silk Saree', product, { brandId: 'brand-001' }), true);
  assert.equal(passesProductFilters('SKU-001', 'Silk Saree', product, { sku: 'OTHER' }), false);
}

function testSalesLineParsing() {
  const payload = {
    billNo: 'BILL-100',
    createdAtUtc: '2026-03-15T10:00:00.000Z',
    salesman: 'Ravi',
    salesmanCode: 'SM-01',
    paymentMode: 'Cash',
    lines: [
      { sku: 'SKU-001', description: 'Product A', qty: 2, rate: 100, amount: 200 },
    ],
  };
  const rows = parseSalesLinesFromPayload(payload, {
    storeId: 'store-001',
    documentNo: 'BILL-100',
    invoiceNo: 'BILL-100',
  });
  assert.equal(rows.length, 1);
  assert.equal(rows[0]?.qty, 2);
  assert.equal(rows[0]?.amount, 200);
  assert.equal(rows[0]?.salesman, 'Ravi');
  assert.equal(rows[0]?.isReturn, false);

  const returnPayload = {
    billNo: 'BILL-100',
    createdAtUtc: '2026-03-16T10:00:00.000Z',
    returnLines: [{ sku: 'SKU-001', description: 'Product A', returnQty: 1, rate: 100, amount: 100 }],
  };
  const returnRows = parseSalesLinesFromPayload(returnPayload, {
    storeId: 'store-001',
    documentNo: 'RET-01',
    invoiceNo: 'BILL-100',
    isReturn: true,
  });
  assert.equal(returnRows.length, 1);
  assert.equal(returnRows[0]?.qty, -1);
  assert.equal(returnRows[0]?.isReturn, true);
}

function testPaginationAndSummary() {
  const all = [1, 2, 3, 4, 5];
  const page = paginateRows(all, 2, 1);
  assert.deepEqual(page.rows, [2, 3]);
  assert.equal(page.truncated, true);

  const summary = buildItemDetailsSummary(
    [{ orderedQty: 10 } as never],
    [{ receivedQty: 8 } as never],
    [{ totalSoh: 5 } as never],
    [{ qty: 3, amount: 300 } as never],
    { poLines: false, grnLines: false, soh: false, sales: false },
    { poLineCount: 1, grnLineCount: 1, sohSkuCount: 1, salesLineCount: 1 },
  );
  assert.equal(summary.totalOrderedQty, 10);
  assert.equal(summary.totalReceivedQty, 8);
  assert.equal(summary.totalSohQty, 5);
  assert.equal(summary.totalSoldQty, 3);
  assert.equal(summary.totalSalesAmount, 300);
}

function testSohSalesEnrichment() {
  const salesQtyBySku = aggregateSalesQtyBySku([
    { sku: 'SKU-001', qty: 5 } as SalesItemRow,
    { sku: 'SKU-001', qty: 2 } as SalesItemRow,
    { sku: 'SKU-002', qty: -1, isReturn: true } as SalesItemRow,
  ]);
  assert.equal(salesQtyBySku.get('sku-001'), 7);
  assert.equal(salesQtyBySku.get('sku-002'), -1);

  const sohRows: SohItemRow[] = [
    {
      sku: 'SKU-001',
      productName: 'Product A',
      warehouseQty: 0,
      inTransitQty: 0,
      storeQty: 10,
      totalSoh: 10,
      salesQty: 0,
      remainingQty: 0,
    },
  ];
  enrichSohRowsWithSalesQty(sohRows, salesQtyBySku);
  assert.equal(sohRows[0]?.salesQty, 7);
  assert.equal(sohRows[0]?.remainingQty, 3);
}

testDateBounds();
testProductFilters();
testSalesLineParsing();
testPaginationAndSummary();
testSohSalesEnrichment();

console.log('item-details-report helper tests passed');
