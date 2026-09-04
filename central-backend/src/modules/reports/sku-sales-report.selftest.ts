/**
 * Grouping, unmapped SKUs, return netting, ranking, and workbook sheets
 * for supplier-wise and fast-sellers reports.
 */
import 'reflect-metadata';
import assert from 'node:assert/strict';
import * as XLSX from 'xlsx';
import { UNMAPPED_VENDOR_ID, UNMAPPED_VENDOR_NAME } from '../dashboard/store-vendors-sales-dashboard.types';
import { FastSellersReportExportService } from './fast-sellers-report-export.service';
import { SupplierWiseSalesReportExportService } from './supplier-wise-sales-report-export.service';
import {
  attachStockLevels,
  collectSkuSales,
  filterSkuRowsForSupplierSearch,
  groupSupplierWise,
  rankFastSellers,
} from './sku-sales-report.util';

function invoice(invoiceNo: string, lines: unknown[], status = 'posted') {
  return {
    invoiceNo,
    payload: { billNo: invoiceNo, status, lines },
  };
}

async function run() {
  const invoices = [
    invoice('B-1', [
      { sku: 'SKU-A', description: 'Saree A', qty: 5, rate: 100, amount: 500 },
      { sku: 'SKU-C', description: 'Saree C', qty: 4, rate: 100, amount: 400 },
    ]),
    invoice('B-2', [{ sku: 'SKU-B', description: 'Saree B', qty: 3, rate: 90, amount: 270 }]),
    invoice('VOID-1', [{ sku: 'SKU-Z', qty: 99, rate: 10, amount: 990 }], 'void'),
  ];
  const returns = [
    {
      returnNo: 'R-1',
      payload: {
        status: 'posted',
        originalBillNo: 'B-1',
        returnLines: [{ sku: 'SKU-A', returnQty: 1, lineTotal: 100 }],
      },
    },
    {
      returnNo: 'R-VOID',
      payload: {
        status: 'void',
        originalBillNo: 'B-1',
        returnLines: [{ sku: 'SKU-C', returnQty: 4, lineTotal: 400 }],
      },
    },
  ];
  const catalog = new Map([
    ['SKU-A', { sku: 'SKU-A', itemName: 'Saree A', supplierNameId: 'sup-1', supplierName: 'Acme Silks' }],
    ['SKU-C', { sku: 'SKU-C', itemName: 'Saree C', supplierNameId: 'sup-1' }],
  ]);
  const supplierNames = new Map([['sup-1', 'Acme Silks']]);

  const collected = collectSkuSales(invoices, returns, catalog, supplierNames);
  const bySku = Object.fromEntries(collected.rows.map((row) => [row.sku, row]));

  assert.equal(bySku['SKU-A']?.soldQty, 5);
  assert.equal(bySku['SKU-A']?.returnQty, 1);
  assert.equal(bySku['SKU-A']?.netQty, 4);
  assert.equal(bySku['SKU-A']?.netAmount, 400);
  assert.equal(bySku['SKU-A']?.supplierName, 'Acme Silks');
  assert.equal(bySku['SKU-B']?.supplierId, UNMAPPED_VENDOR_ID);
  assert.equal(bySku['SKU-B']?.supplierName, UNMAPPED_VENDOR_NAME);
  assert.equal(bySku['SKU-B']?.netQty, 3);
  assert.equal(bySku['SKU-C']?.supplierName, 'Acme Silks');
  assert.equal(bySku['SKU-Z'], undefined);
  assert.equal(collected.totals.skuCount, 3);
  assert.equal(collected.totals.netQty, 11);
  assert.equal(collected.totals.netAmount, 1070);

  const grouped = groupSupplierWise(collected.rows);
  assert.equal(grouped.data.length, 2);
  assert.equal(grouped.data[0]?.supplierName, 'Acme Silks');
  assert.equal(grouped.data[0]?.productCount, 2);
  assert.equal(grouped.data[0]?.netQty, 8);
  assert.equal(grouped.data[1]?.supplierName, UNMAPPED_VENDOR_NAME);
  assert.equal(grouped.data[1]?.products[0]?.sku, 'SKU-B');

  const searched = filterSkuRowsForSupplierSearch(collected.rows, 'acme');
  assert.deepEqual(
    searched.map((row) => row.sku).sort(),
    ['SKU-A', 'SKU-C'],
  );

  const ranked = rankFastSellers(collected.rows);
  assert.deepEqual(
    ranked.map((row) => row.sku),
    ['SKU-A', 'SKU-C', 'SKU-B'],
  );

  const withStock = attachStockLevels(
    ranked,
    new Map([
      ['SKU-A', 3],
      ['SKU-C', 20],
    ]),
    5,
  );
  assert.equal(withStock[0]?.availableQty, 3);
  assert.equal(withStock[0]?.isLowStock, true);
  assert.equal(withStock[1]?.availableQty, 20);
  assert.equal(withStock[1]?.isLowStock, false);
  assert.equal(withStock[2]?.availableQty, 0);
  assert.equal(withStock[2]?.isLowStock, true);
  const neverFlagged = attachStockLevels(ranked.slice(0, 1), new Map([['SKU-A', 0]]), 0);
  assert.equal(neverFlagged[0]?.isLowStock, false);

  const period = {
    from: '2026-08-01',
    to: '2026-08-31',
    timezone: 'Asia/Kolkata',
    storeCode: 'store-1',
    storeName: 'Main Store',
  };
  const companyModel = {
    findOne: () => ({ lean: async () => ({ tradeName: 'RR Bridal', city: 'Chennai' }) }),
  };

  const supplierReport = {
    period,
    filters: {},
    limit: 10000,
    truncated: false,
    total: 2,
    totals: { ...collected.totals, supplierCount: grouped.data.length },
    data: grouped.data,
  };
  const supplierExport = new SupplierWiseSalesReportExportService(
    { buildReport: async () => supplierReport } as never,
    companyModel as never,
  );
  const supplierFile = await supplierExport.buildExport({ from: '2026-08-01', to: '2026-08-31' });
  const supplierBook = XLSX.read(supplierFile.buffer, { type: 'buffer' });
  assert.deepEqual(supplierBook.SheetNames, ['Summary', 'Product Details']);
  assert.match(supplierFile.filename, /^supplier-wise-store-1-2026-08-01-to-2026-08-31-\d{4}-\d{2}-\d{2}\.xlsx$/);
  const summaryCells = XLSX.utils.sheet_to_json<string[]>(supplierBook.Sheets.Summary!, { header: 1 }).flat();
  assert.ok(summaryCells.some((cell) => String(cell).includes('Acme Silks')));
  assert.ok(summaryCells.some((cell) => String(cell).includes('No vendor mapped')));
  const detailCells = XLSX.utils.sheet_to_json<string[]>(supplierBook.Sheets['Product Details']!, {
    header: 1,
  }).flat();
  assert.ok(detailCells.some((cell) => String(cell) === 'SKU-A'));
  assert.ok(detailCells.some((cell) => String(cell) === 'SKU-B'));

  const fastData = withStock.map((row, index) => ({ ...row, rank: index + 1 }));
  const fastReport = {
    period,
    filters: {},
    limit: 100,
    truncated: false,
    total: fastData.length,
    totals: collected.totals,
    data: fastData,
  };
  const fastExport = new FastSellersReportExportService(
    { buildReport: async () => fastReport } as never,
    companyModel as never,
  );
  const fastFile = await fastExport.buildExport({ from: '2026-08-01', to: '2026-08-31' });
  const fastBook = XLSX.read(fastFile.buffer, { type: 'buffer' });
  assert.deepEqual(fastBook.SheetNames, ['Fast Sellers']);
  assert.match(fastFile.filename, /^fast-sellers-store-1-2026-08-01-to-2026-08-31-\d{4}-\d{2}-\d{2}\.xlsx$/);
  const fastCells = XLSX.utils.sheet_to_json<string[]>(fastBook.Sheets['Fast Sellers']!, { header: 1 }).flat();
  assert.ok(fastCells.some((cell) => String(cell) === 'SKU-A'));
  assert.ok(fastCells.some((cell) => String(cell) === '1'));
  assert.ok(fastCells.some((cell) => String(cell) === 'Available Qty'));
  assert.ok(fastCells.some((cell) => String(cell) === 'Low Stock'));
  assert.ok(fastCells.some((cell) => String(cell) === 'Yes'));

  console.log('sku-sales-report.selftest: ok');
}

void run();
