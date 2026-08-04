import assert from 'node:assert/strict';
import { after, before, describe, it } from 'node:test';
import { MongoMemoryServer } from 'mongodb-memory-server';
import { ObjectId } from 'mongodb';
import { BusinessService } from '../src/business/business.service';
import { ConnectionRegistryService } from '../src/database/connection-registry.service';
import { StorefrontService } from '../src/storefront/storefront.service';

describe('multi-database business service', () => {
  let server: MongoMemoryServer;
  let registry: ConnectionRegistryService;
  let service: BusinessService;
  let storefront: StorefrontService;

  before(async () => {
    server = await MongoMemoryServer.create();
    process.env.B2B_DATABASES = JSON.stringify([
      { key: 'one', label: 'Database One', uri: server.getUri('one'), publicApiBaseUrl: 'https://one.example' },
      { key: 'two', label: 'Database Two', uri: server.getUri('two'), publicApiBaseUrl: 'https://two.example' },
      { key: 'three', label: 'Database Three', uri: server.getUri('three'), publicApiBaseUrl: 'https://three.example' },
    ]);
    registry = new ConnectionRegistryService();
    service = new BusinessService(registry);
    storefront = new StorefrontService(registry);

    const quantities = [5, 8, 13];
    for (const [index, key] of ['one', 'two', 'three'].entries()) {
      const db = await registry.database(key);
      const categoryId = new ObjectId();
      await db.collection('categories').insertOne({ _id: categoryId, code: 'cat-001', name: 'Bridal Wear', isActive: true });
      await db.collection('products').insertOne({
        sku: 'SKU-1',
        itemName: `Product ${key}`,
        costPrice: 10,
        sellingPrice: 20,
        categoryId,
        mediaItems: [{ url: '/api/media/files/products/sku-1.webp' }],
        isActive: true,
        isAddedInB2B: true,
      });
      await db.collection('inventoryledgerentries').insertOne({
        sku: 'SKU-1',
        qtyDelta: quantities[index],
        locationKind: 'warehouse',
        locationCode: 'main',
        sourceType: 'Seed',
        sourceId: `seed-${key}`,
        createdAt: new Date(),
        updatedAt: new Date(),
      });
    }
    await (await registry.database('one')).collection('products').insertOne({
      sku: 'HIDDEN-1',
      itemName: 'Not published',
      sellingPrice: 99,
      isActive: true,
    });
  });

  after(async () => {
    await registry.onApplicationShutdown();
    await server.stop();
  });

  it('keeps inventory results isolated and database-labelled', async () => {
    const response = await service.inventorySummary({ databaseKey: 'all', page: 1, limit: 50 });
    assert.deepEqual(response.errors, []);
    assert.deepEqual(
      response.results
        .sort((a, b) => a.databaseKey.localeCompare(b.databaseKey))
        .map((result) => [result.databaseKey, result.data.units]),
      [
      ['one', 5],
      ['three', 13],
      ['two', 8],
      ],
    );
  });

  it('routes a write once to the selected database', async () => {
    const adjustment = {
      databaseKey: 'two',
      sku: 'SKU-1',
      qtyDelta: 2,
      locationKind: 'warehouse' as const,
      locationCode: 'main',
      sourceId: 'b2b-test-adjustment',
    };
    assert.equal((await service.adjustInventory(adjustment)).created, true);
    assert.equal((await service.adjustInventory(adjustment)).created, false);

    const one = await service.inventorySummary({ databaseKey: 'one', page: 1, limit: 50 });
    const two = await service.inventorySummary({ databaseKey: 'two', page: 1, limit: 50 });
    assert.equal(one.results[0].data.units, 5);
    assert.equal(two.results[0].data.units, 10);
  });

  it('publishes only opted-in products as separate database offers', async () => {
    const response = await storefront.catalog({ page: 1, limit: 24, sort: 'featured' });
    assert.equal(response.total, 3);
    assert.deepEqual(response.data.map((offer) => offer.databaseKey), ['one', 'three', 'two']);
    assert.equal(response.data.some((offer) => offer.sku === 'HIDDEN-1'), false);
    assert.equal(response.data[0].category.key, 'bridal-wear');
    assert.equal(response.data[0].images[0], 'https://one.example/api/media/files/products/sku-1.webp');
    const categories = await storefront.categories();
    assert.deepEqual(categories.data, [{ key: 'bridal-wear', name: 'Bridal Wear', productCount: 3 }]);
  });

  it('splits and idempotently records enquiries by source database', async () => {
    const catalog = await storefront.catalog({ page: 1, limit: 24, sort: 'featured' });
    const lines = catalog.data.slice(0, 2).map((offer) => ({
      databaseKey: offer.databaseKey,
      productId: offer.productId,
      quantity: 1,
    }));
    const payload = {
      requestId: 'storefront-request-1',
      retailer: {
        businessName: 'Test Retailer',
        contactName: 'Buyer',
        email: 'buyer@example.com',
        phone: '9000000000',
      },
      lines,
    };
    const first = await storefront.submitEnquiry(payload);
    const retry = await storefront.submitEnquiry(payload);
    assert.equal(first.results.length, 2);
    assert.equal(first.results.every((result) => result.created), true);
    assert.equal(retry.results.every((result) => !result.created), true);
    for (const line of lines) {
      const db = await registry.database(line.databaseKey);
      assert.equal(await db.collection('b2b_enquiries').countDocuments({ requestId: payload.requestId }), 1);
      assert.equal(await db.collection('customers').countDocuments({ email: payload.retailer.email }), 1);
    }
  });

  it('returns healthy results alongside a failed source', async () => {
    await registry.onApplicationShutdown();
    process.env.B2B_MONGO_TIMEOUT_MS = '50';
    process.env.B2B_DATABASES = JSON.stringify([
      { key: 'healthy', label: 'Healthy', uri: server.getUri('healthy') },
      { key: 'offline', label: 'Offline', uri: 'mongodb://127.0.0.1:1/offline' },
    ]);
    registry = new ConnectionRegistryService();
    service = new BusinessService(registry);
    storefront = new StorefrontService(registry);
    await (await registry.database('healthy')).collection('inventoryledgerentries').insertOne({
      sku: 'SKU-X',
      qtyDelta: 1,
      sourceType: 'Seed',
      sourceId: 'partial',
    });
    const response = await service.inventorySummary({ databaseKey: 'all', page: 1, limit: 50 });
    assert.equal(response.results.length, 1);
    assert.deepEqual(response.errors, [
      { databaseKey: 'offline', databaseLabel: 'Offline', error: 'Database is unavailable' },
    ]);
  });
});
