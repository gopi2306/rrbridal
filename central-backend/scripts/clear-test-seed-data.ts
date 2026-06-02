/**
 * Removes flow-wise test data (SKU-001…005, PO-1001, promos, etc.).
 * Does not remove users, stores, or company profile.
 *
 * Usage: npm run seed:clear-test-data
 */
import 'reflect-metadata';

/** Avoid re-inserting test rows during app bootstrap. */
process.env.SEED_TEST_DATA = 'false';

import { NestFactory } from '@nestjs/core';
import { AppModule } from '../src/modules/app/app.module';
import { TestDataSeedService } from '../src/modules/seed/test-data-seed.service';

async function main() {
  const app = await NestFactory.createApplicationContext(AppModule, { logger: ['log', 'error', 'warn'] });
  await app.get(TestDataSeedService).clearTestData();
  await app.close();
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
