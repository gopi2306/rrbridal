/**
 * Self-contained unit checks for product sync cursor (no Jest yet).
 * Run: npx ts-node -r reflect-metadata src/modules/sync/product-sync-cursor.selftest.ts
 */
import assert from 'node:assert/strict';
import { Types } from 'mongoose';
import {
  buildProductDeltaFilter,
  encodeProductSyncCursor,
  encodeProductSyncCursorFromProduct,
  parseProductSyncCursor,
} from './product-sync-cursor';

function run() {
  assert.deepEqual(parseProductSyncCursor('0'), { kind: 'start' });
  assert.deepEqual(parseProductSyncCursor(''), { kind: 'start' });

  const legacyId = new Types.ObjectId().toHexString();
  assert.equal(parseProductSyncCursor(legacyId).kind, 'full-catchup');
  assert.deepEqual(buildProductDeltaFilter(parseProductSyncCursor(legacyId)), {});

  const t1 = new Date('2026-07-01T10:00:00.000Z');
  const id1 = new Types.ObjectId();
  const encoded = encodeProductSyncCursor(t1, id1);
  assert.equal(encoded, `${t1.toISOString()}|${id1.toHexString()}`);

  const parsed = parseProductSyncCursor(encoded);
  assert.equal(parsed.kind, 'compound');
  if (parsed.kind === 'compound') {
    assert.equal(parsed.updatedAt.toISOString(), t1.toISOString());
    assert.equal(parsed.id.toHexString(), id1.toHexString());
  }

  const filter = buildProductDeltaFilter(parseProductSyncCursor(encoded));
  assert.deepEqual(filter, {
    $or: [
      { updatedAt: { $gt: t1 } },
      { updatedAt: t1, _id: { $gt: id1 } },
    ],
  });

  const later = new Date('2026-07-01T11:00:00.000Z');
  const cursorAfterFirst = encodeProductSyncCursorFromProduct({ _id: id1, updatedAt: t1 }, '0');
  const filter2 = buildProductDeltaFilter(parseProductSyncCursor(cursorAfterFirst));
  assert.ok(later > t1);
  assert.ok('$or' in filter2);

  // Price-update scenario: same _id, newer updatedAt must pass filter after older cursor.
  const priceUpdatedAt = new Date('2026-07-15T12:00:00.000Z');
  const afterInitialPull = encodeProductSyncCursor(t1, id1);
  const afterFilter = buildProductDeltaFilter(parseProductSyncCursor(afterInitialPull));
  const orBranches = (afterFilter as { $or: Array<Record<string, unknown>> }).$or;
  assert.equal(orBranches.length, 2);
  assert.deepEqual(orBranches[0], { updatedAt: { $gt: t1 } });
  assert.ok(priceUpdatedAt.getTime() > t1.getTime());

  console.log('product-sync-cursor.selftest: ok');
}

run();
