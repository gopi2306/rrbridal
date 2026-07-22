/**
 * Self-contained unit checks for product media items (no Jest yet).
 * Run: npx ts-node -r reflect-metadata src/modules/products/product-media-items.selftest.ts
 */
import assert from 'node:assert/strict';
import {
  appendOrUpdateMediaItem,
  mediaItemsToLegacyUrls,
  normalizeMediaItemsInput,
  resolveProductMediaItems,
} from './product-media-items';

function run() {
  // Legacy mediaUrls → mediaItems
  const fromLegacy = resolveProductMediaItems({
    mediaUrls: ['/api/media/files/products/a.jpg', '/media/files/products/b.png'],
  });
  assert.equal(fromLegacy.length, 2);
  assert.equal(fromLegacy[0]?.url, '/api/media/files/products/a.jpg');
  assert.equal(fromLegacy[0]?.description, undefined);
  assert.equal(fromLegacy[1]?.url, '/api/media/files/products/b.png');

  // Prefer mediaItems over legacy mediaUrls
  const preferNew = resolveProductMediaItems({
    mediaItems: [{ url: '/api/media/files/products/new.jpg', description: 'Primary' }],
    mediaUrls: ['/api/media/files/products/old.jpg'],
  });
  assert.equal(preferNew.length, 1);
  assert.equal(preferNew[0]?.description, 'Primary');

  // Empty sources
  assert.deepEqual(resolveProductMediaItems({}), []);

  // Normalization dedupes by URL; last description wins
  const deduped = normalizeMediaItemsInput([
    { url: '/api/media/files/products/x.jpg', description: 'First' },
    { url: '/api/media/files/products/x.jpg', description: 'Second' },
    { url: '/api/media/files/products/y.jpg' },
  ]);
  assert.ok(deduped);
  assert.equal(deduped.length, 2);
  assert.equal(deduped[0]?.description, 'Second');
  assert.equal(deduped[1]?.url, '/api/media/files/products/y.jpg');

  // Append new URL
  const withNew = appendOrUpdateMediaItem(
    [{ url: '/api/media/files/products/a.jpg' }],
    '/api/media/files/products/b.jpg',
    'Side view',
  );
  assert.equal(withNew.length, 2);
  assert.equal(withNew[1]?.description, 'Side view');

  // Duplicate URL updates description
  const updated = appendOrUpdateMediaItem(
    [{ url: '/api/media/files/products/a.jpg', description: 'Old' }],
    '/api/media/files/products/a.jpg',
    'New note',
  );
  assert.equal(updated.length, 1);
  assert.equal(updated[0]?.description, 'New note');

  // Duplicate URL without new description leaves existing description
  const unchanged = appendOrUpdateMediaItem(
    [{ url: '/api/media/files/products/a.jpg', description: 'Keep' }],
    '/api/media/files/products/a.jpg',
  );
  assert.equal(unchanged.length, 1);
  assert.equal(unchanged[0]?.description, 'Keep');

  assert.deepEqual(mediaItemsToLegacyUrls(withNew), [
    '/api/media/files/products/a.jpg',
    '/api/media/files/products/b.jpg',
  ]);

  console.log('product-media-items.selftest: ok');
}

run();
