/**
 * Threshold priority, critical vs low, missing ledger = 0, matchQty fallback, search, workbook sheet.
 */
import 'reflect-metadata';
import assert from 'node:assert/strict';
import * as XLSX from 'xlsx';
import { GoingOutOfStockReportExportService } from './going-out-of-stock-report-export.service';
import {
  collectGoingOutOfStock,
  filterGoingOutOfStockRows,
  getShelfThreshold,
  totalsGoingOutOfStock,
} from './going-out-of-stock-report.util';

async function run() {
  assert.equal(
    getShelfThreshold({
      sku: 'A',
      itemName: 'A',
      minimumShelfFit: 3,
      minStock: 1,
      reorderLevel: 5,
    }),
    3,
  );
  assert.equal(
    getShelfThreshold({ sku: 'A', itemName: 'A', minStock: 1, reorderLevel: 5 }),
    1,
  );
  assert.equal(getShelfThreshold({ sku: 'A', itemName: 'A', reorderLevel: 5 }), 5);
  assert.equal(getShelfThreshold({ sku: 'A', itemName: 'A' }), undefined);
  assert.equal(getShelfThreshold({ sku: 'A', itemName: 'A', reorderLevel: 0 }, 5), 5);
  assert.equal(getShelfThreshold({ sku: 'A', itemName: 'A', minStock: 2 }, 5), 2);

  const products = [
    {
      sku: 'SKU-CRIT',
      itemName: 'Critical item',
      minimumShelfFit: 5,
      minStock: 2,
      supplierId: 'sup-1',
      supplierName: 'Acme',
    },
    {
      sku: 'SKU-LOW',
      itemName: 'Low item',
      minimumShelfFit: 5,
      minStock: 1,
      supplierId: 'sup-1',
      supplierName: 'Acme',
    },
    {
      sku: 'SKU-OK',
      itemName: 'Healthy item',
      reorderLevel: 2,
      supplierId: 'sup-2',
      supplierName: 'Beta',
    },
    {
      sku: 'SKU-MISS',
      itemName: 'Missing ledger',
      reorderLevel: 1,
    },
    {
      sku: 'SKU-NONE',
      itemName: 'No threshold',
    },
  ];
  const qty = new Map([
    ['SKU-CRIT', 1],
    ['SKU-LOW', 3],
    ['SKU-OK', 10],
  ]);

  const rows = collectGoingOutOfStock(products, qty);
  assert.deepEqual(
    rows.map((row) => row.sku),
    ['SKU-MISS', 'SKU-CRIT', 'SKU-LOW'],
  );
  assert.equal(rows[0]?.storeQty, 0);
  assert.equal(rows[0]?.status, 'critical');
  assert.equal(rows[0]?.supplierName, 'No vendor mapped');
  assert.equal(rows[1]?.status, 'critical');
  assert.equal(rows[2]?.status, 'low');
  assert.equal(rows.find((row) => row.sku === 'SKU-OK'), undefined);
  assert.equal(rows.find((row) => row.sku === 'SKU-NONE'), undefined);

  const withMatch = collectGoingOutOfStock(
    [
      { sku: 'SKU-FALLBACK', itemName: 'Uses settings' },
      { sku: 'SKU-ZERO', itemName: 'Zero reorder', reorderLevel: 0 },
      { sku: 'SKU-ABOVE', itemName: 'Above' },
      { sku: 'SKU-PRODUCT', itemName: 'Product wins', minStock: 2 },
    ],
    new Map([
      ['SKU-FALLBACK', 3],
      ['SKU-ZERO', 4],
      ['SKU-ABOVE', 6],
      ['SKU-PRODUCT', 2],
    ]),
    5,
  );
  assert.deepEqual(
    withMatch.map((row) => row.sku),
    ['SKU-PRODUCT', 'SKU-FALLBACK', 'SKU-ZERO'],
  );
  assert.equal(withMatch.find((row) => row.sku === 'SKU-FALLBACK')?.threshold, 5);
  assert.equal(withMatch.find((row) => row.sku === 'SKU-PRODUCT')?.threshold, 2);
  assert.equal(withMatch.find((row) => row.sku === 'SKU-ABOVE'), undefined);

  const searched = filterGoingOutOfStockRows(rows, { search: 'critical' });
  assert.equal(searched.length, 1);
  assert.equal(searched[0]?.sku, 'SKU-CRIT');

  const criticalOnly = filterGoingOutOfStockRows(rows, { status: 'critical' });
  assert.deepEqual(
    criticalOnly.map((row) => row.sku),
    ['SKU-MISS', 'SKU-CRIT'],
  );

  const totals = totalsGoingOutOfStock(rows);
  assert.equal(totals.skuCount, 3);
  assert.equal(totals.criticalCount, 2);
  assert.equal(totals.lowCount, 1);
  assert.equal(totals.zeroQtyCount, 1);

  const report = {
    period: {
      asOf: '2026-08-14',
      timezone: 'Asia/Kolkata',
      storeCode: 'store-1',
      storeName: 'Main Store',
    },
    filters: {},
    limit: 10000,
    truncated: false,
    total: rows.length,
    totals,
    data: rows,
  };
  const exportService = new GoingOutOfStockReportExportService(
    { buildReport: async () => report } as never,
    {
      findOne: () => ({ lean: async () => ({ tradeName: 'RR Bridal', city: 'Chennai' }) }),
    } as never,
  );
  const exported = await exportService.buildExport({});
  const workbook = XLSX.read(exported.buffer, { type: 'buffer' });
  assert.deepEqual(workbook.SheetNames, ['Going Out Of Stock']);
  assert.match(exported.filename, /^going-out-of-stock-store-1-2026-08-14-\d{4}-\d{2}-\d{2}\.xlsx$/);
  const cells = XLSX.utils
    .sheet_to_json<string[]>(workbook.Sheets['Going Out Of Stock']!, { header: 1 })
    .flat();
  assert.ok(cells.some((cell) => String(cell) === 'SKU-MISS'));
  assert.ok(cells.some((cell) => String(cell) === 'critical'));

  console.log('going-out-of-stock-report.selftest: ok');
}

void run();
