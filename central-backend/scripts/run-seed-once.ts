/**
 * One-off seed runner (company profile + store receipt print settings).
 * Usage: npx ts-node -r reflect-metadata scripts/run-seed-once.ts
 */
import 'reflect-metadata';
import { NestFactory } from '@nestjs/core';
import { AppModule } from '../src/modules/app/app.module';
import { CompanyProfileSeedService } from '../src/modules/company-profile/company-profile-seed.service';

async function main() {
  process.env.SEED_SKIP_COMPANY_PROFILE = 'false';
  process.env.SEED_FORCE_COMPANY_PROFILE = process.env.SEED_FORCE_COMPANY_PROFILE ?? 'true';
  const app = await NestFactory.createApplicationContext(AppModule, { logger: ['log', 'error', 'warn'] });
  const seeder = app.get(CompanyProfileSeedService);
  await seeder.runSeed();
  await app.close();
  console.log('Seed run finished.');
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
