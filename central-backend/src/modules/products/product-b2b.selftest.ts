/**
 * Focused checks for B2B product filtering and spreadsheet boolean parsing.
 * Run: npx ts-node -r reflect-metadata src/modules/products/product-b2b.selftest.ts
 */
import assert from 'node:assert/strict';
import { plainToInstance } from 'class-transformer';
import { validateSync } from 'class-validator';
import { FilterQuery } from 'mongoose';
import { ListProductsQueryDto } from './dto/list-products-query.dto';
import { rowArraysToParsedRows } from './import/product-import-columns';
import { applyB2BProductFilter } from './products.service';
import { ProductDocument } from './schemas/product.schema';

function run() {
  const explicitTrue: FilterQuery<ProductDocument> = {};
  applyB2BProductFilter(explicitTrue, true);
  assert.deepEqual(explicitTrue, { isAddedInB2B: true });

  const falseIncludingLegacy: FilterQuery<ProductDocument> = {};
  applyB2BProductFilter(falseIncludingLegacy, false);
  assert.deepEqual(falseIncludingLegacy, { isAddedInB2B: { $ne: true } });

  const unfiltered: FilterQuery<ProductDocument> = {};
  applyB2BProductFilter(unfiltered, undefined);
  assert.deepEqual(unfiltered, {});

  const trueQuery = plainToInstance(ListProductsQueryDto, { isAddedInB2B: 'true' });
  const falseQuery = plainToInstance(ListProductsQueryDto, { isAddedInB2B: 'false' });
  assert.equal(trueQuery.isAddedInB2B, true);
  assert.equal(falseQuery.isAddedInB2B, false);
  assert.deepEqual(validateSync(trueQuery), []);
  assert.deepEqual(validateSync(falseQuery), []);

  const [parsed] = rowArraysToParsedRows(['itemName', 'isAddedInB2B'], [['Dress', 'yes']]);
  assert.equal(parsed?.isAddedInB2B, true);

  console.log('product-b2b self-test: OK');
}

run();
