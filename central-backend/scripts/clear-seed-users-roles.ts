/**
 * Removes demo users (store, ops-admin, etc.) and non–super_admin roles.
 * Keeps super_admin role + admin@gmail.com (creates if missing).
 *
 * Usage: npm run seed:clear-users-roles
 */
import 'reflect-metadata';

process.env.SEED_TEST_DATA = 'false';
process.env.SEED_PRODUCTION = 'true';

import { NestFactory } from '@nestjs/core';
import { AppModule } from '../src/modules/app/app.module';
import { UsersSeedService } from '../src/modules/users/users-seed.service';

async function main() {
  const app = await NestFactory.createApplicationContext(AppModule, { logger: ['log', 'error', 'warn'] });
  await app.get(UsersSeedService).clearDevUsersAndRoles();
  await app.close();
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
