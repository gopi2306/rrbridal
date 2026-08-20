/**
 * Focused checks for GST-exclusive product price derivation and backfill.
 * Run: npx ts-node -r reflect-metadata src/modules/products/product-price-gst.selftest.ts
 */
import assert from 'node:assert/strict';
import { Types } from 'mongoose';
import {
  deriveWithoutGstPrices,
  hasWithoutGstPriceSourceChanged,
  priceWithoutGst,
} from './product-price-gst.util';
import { ProductsService } from './products.service';

function testPriceDerivation() {
  assert.equal(priceWithoutGst(118, 18), 100);
  assert.equal(priceWithoutGst(100, 0), 100);
  assert.equal(priceWithoutGst(99.99, 18), 84.7373);

  assert.deepEqual(
    deriveWithoutGstPrices({
      gstPercent: 18,
      costPrice: 118,
      mrp: 236,
      sellingPrice: 177,
      storePrice: 141.6,
    }),
    {
      costPriceWithoutGst: 100,
      mrpWithoutGst: 200,
      sellingPriceWithoutGst: 150,
      storePriceWithoutGst: 120,
    },
  );
  assert.equal(deriveWithoutGstPrices({ costPrice: 118 }), null);
  assert.equal(deriveWithoutGstPrices({ gstPercent: -1, costPrice: 118 }), null);
}

function testUpdateMergeDerivation() {
  const before = { gstPercent: 5, mrp: 105, sellingPrice: 84 };
  const gstPatch = { gstPercent: 12 };
  assert.equal(hasWithoutGstPriceSourceChanged(gstPatch), true);
  assert.deepEqual(deriveWithoutGstPrices({ ...before, ...gstPatch }), {
    mrpWithoutGst: 93.75,
    sellingPriceWithoutGst: 75,
  });
  assert.equal(hasWithoutGstPriceSourceChanged({ itemName: 'Updated' }), false);
}

async function testCreateAndUpdateWrites() {
  const productId = new Types.ObjectId();
  let createdPayload: Record<string, unknown> = {};
  let updateOps: Record<string, unknown> = {};
  const before = {
    _id: productId,
    sku: 'SKU-000001',
    itemName: 'Dress',
    gstPercent: 5,
    mrp: 105,
    sellingPrice: 84,
  };
  const productModel = {
    create: async (payload: Record<string, unknown>) => {
      createdPayload = payload;
      return {
        _id: productId,
        sku: payload.sku,
        toObject: () => ({ _id: productId, ...payload }),
      };
    },
    findById: () => ({ lean: async () => before }),
    findByIdAndUpdate: (_id: Types.ObjectId, operations: Record<string, unknown>) => {
      updateOps = operations;
      return {
        lean: async () => ({
          ...before,
          ...((operations.$set as Record<string, unknown>) ?? {}),
        }),
      };
    },
  };
  const skuGenerator = { allocateNextAsync: async () => 'SKU-000001' };
  const auditLogs = { logProductChange: async () => undefined };
  const service = new ProductsService(
    productModel as never,
    skuGenerator as never,
    auditLogs as never,
  );

  await service.create({
    itemName: 'Dress',
    supplierNameId: '507f1f77bcf86cd799439011',
    departmentId: '507f1f77bcf86cd799439012',
    categoryId: '507f1f77bcf86cd799439013',
    gstPercent: 18,
    costPrice: 118,
    mrp: 236,
    sellingPrice: 177,
    storePrice: 141.6,
  });
  assert.equal(createdPayload.costPriceWithoutGst, 100);
  assert.equal(createdPayload.mrpWithoutGst, 200);
  assert.equal(createdPayload.sellingPriceWithoutGst, 150);
  assert.equal(createdPayload.storePriceWithoutGst, 120);

  await service.update(String(productId), { gstPercent: 12 });
  assert.deepEqual((updateOps.$set as Record<string, unknown>) ?? {}, {
    gstPercent: 12,
    mrpWithoutGst: 93.75,
    sellingPriceWithoutGst: 75,
  });
}

async function testBackfillCounts() {
  const firstId = new Types.ObjectId();
  const secondId = new Types.ObjectId();
  const rows = [
    {
      _id: firstId,
      gstPercent: 18,
      costPrice: 118,
      mrp: 236,
      sellingPrice: 177,
      storePrice: 141.6,
    },
    {
      _id: secondId,
      costPrice: 100,
    },
  ];
  let findCount = 0;
  let capturedOperations: unknown[] = [];
  const productModel = {
    find: () => ({
      select: () => ({
        sort: () => ({
          limit: () => ({
            lean: async () => {
              findCount += 1;
              return findCount === 1 ? rows : [];
            },
          }),
        }),
      }),
    }),
    bulkWrite: async (operations: unknown[]) => {
      capturedOperations = operations;
      return { modifiedCount: operations.length };
    },
  };

  const service = new ProductsService(productModel as never, {} as never, {} as never);
  assert.deepEqual(await service.backfillWithoutGstPrices(), {
    processed: 2,
    updated: 1,
    skippedMissingGst: 1,
  });
  assert.equal(capturedOperations.length, 1);
  assert.deepEqual(capturedOperations[0], {
    updateOne: {
      filter: { _id: firstId },
      update: {
        $set: {
          costPriceWithoutGst: 100,
          mrpWithoutGst: 200,
          sellingPriceWithoutGst: 150,
          storePriceWithoutGst: 120,
        },
      },
    },
  });
}

async function run() {
  testPriceDerivation();
  testUpdateMergeDerivation();
  await testCreateAndUpdateWrites();
  await testBackfillCounts();
  console.log('product price GST self-test: OK');
}

void run();
