import assert from 'node:assert/strict';
import * as XLSX from 'xlsx';
import {
  PHYSICAL_INVENTORY_HEADERS,
  parsePhysicalInventoryRows,
} from './import/physical-inventory-import-columns';
import { PhysicalInventoryImportService } from './import/physical-inventory-import.service';
import { physicalInventoryImportEventId } from './inventory-adjustments.service';

async function run() {
  assert.notEqual(
    physicalInventoryImportEventId('store-001', 'batch-1'),
    physicalInventoryImportEventId('store-002', 'batch-1'),
  );
  assert.equal(
    physicalInventoryImportEventId(' STORE-001 ', ' batch-1 '),
    physicalInventoryImportEventId('store-001', 'batch-1'),
  );
  const parsed = parsePhysicalInventoryRows(
    ['sku', 'Item Name', 'Physical Quantity'],
    [
      ['SKU-1', 'One', '5'],
      ['SKU-2', 'Two', ''],
      ['SKU-1', 'One duplicate', '6'],
      ['SKU-3', 'Three', '-1'],
      ['SKU-0', 'Zero', '0'],
    ],
  );
  assert.equal(parsed.rows.length, 2);
  assert.equal(parsed.rows[0]?.newQty, 5);
  assert.equal(parsed.rows[1]?.newQty, 0);
  assert.equal(parsed.sourceRows, 4);
  assert.equal(parsed.errors.length, 2);

  const products = {
    listPhysicalInventoryTemplateProducts: async () => [
      { sku: 'SKU-1', itemName: 'One' },
      { sku: 'SKU-2', itemName: 'Two' },
    ],
    findPhysicalInventoryProductsBySkus: async (skus: string[]) =>
      skus.filter((sku) => sku === 'SKU-1').map((sku) => ({ sku, itemName: 'One' })),
  };
  let commitBatchId = '';
  const adjustments = {
    validateStoreCode: async (storeCode: string) => storeCode,
    previewStorePhysicalCount: async (_storeCode: string, lines: Array<{ sku: string; newQty: number }>) => ({
      storeId: 'store-001',
      lines: lines.map((line) => ({
        sku: line.sku,
        itemName: 'One',
        qtyBefore: 3,
        newQty: line.newQty,
        qtyDelta: line.newQty - 3,
        qtyAfter: line.newQty,
        unchanged: line.newQty === 3,
      })),
    }),
    createStorePhysicalImport: async (input: { batchId: string }) => {
      commitBatchId = input.batchId;
      return { id: 'adjustment-1', adjustmentNo: 'IA-1' };
    },
  };
  const service = new PhysicalInventoryImportService(products as never, adjustments as never);
  const template = await service.buildTemplate('store-001');
  const templateBook = XLSX.read(template, { type: 'buffer' });
  const templateSheet = templateBook.Sheets[templateBook.SheetNames[0]!]!;
  const templateRows = XLSX.utils.sheet_to_json<unknown[]>(templateSheet, {
    header: 1,
    defval: '',
  }) as unknown[][];
  assert.deepEqual(templateRows[0], [...PHYSICAL_INVENTORY_HEADERS]);
  assert.equal(templateRows[1]?.[2], '');

  const uploadSheet = XLSX.utils.aoa_to_sheet([
    [...PHYSICAL_INVENTORY_HEADERS],
    ['SKU-1', 'One', 5],
    ['SKU-2', 'Two', ''],
  ]);
  const uploadBook = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(uploadBook, uploadSheet, 'Physical Inventory');
  const buffer = Buffer.from(XLSX.write(uploadBook, { type: 'buffer', bookType: 'xlsx' }));
  const dryRun = await service.runFromExcelBuffer({
    buffer,
    originalName: 'count.xlsx',
    storeCode: 'store-001',
    reason: 'Physical count',
    dryRun: true,
  });
  assert.equal(dryRun.adjusted, 1);
  assert.equal(dryRun.failed, 0);
  assert.equal(dryRun.readyToCommit, true);

  const committed = await service.runFromExcelBuffer({
    buffer,
    originalName: 'count.xlsx',
    storeCode: 'store-001',
    reason: 'Physical count',
    dryRun: false,
    batchId: 'batch-1',
  });
  assert.equal(commitBatchId, 'batch-1');
  assert.equal(committed.adjustmentId, 'adjustment-1');
}

void run()
  .then(() => console.log('physical-inventory-import.selftest: ok'))
  .catch((error) => {
    console.error(error);
    process.exitCode = 1;
  });
