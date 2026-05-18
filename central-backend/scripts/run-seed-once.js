"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
/**
 * One-off seed runner (company profile + store receipt print settings).
 * Usage: npx ts-node -r reflect-metadata scripts/run-seed-once.ts
 */
require("reflect-metadata");
const core_1 = require("@nestjs/core");
const app_module_1 = require("../src/modules/app/app.module");
const company_profile_seed_service_1 = require("../src/modules/company-profile/company-profile-seed.service");
async function main() {
    process.env.SEED_SKIP_COMPANY_PROFILE = 'false';
    process.env.SEED_FORCE_COMPANY_PROFILE = process.env.SEED_FORCE_COMPANY_PROFILE ?? 'true';
    const app = await core_1.NestFactory.createApplicationContext(app_module_1.AppModule, { logger: ['log', 'error', 'warn'] });
    const seeder = app.get(company_profile_seed_service_1.CompanyProfileSeedService);
    await seeder.runSeed();
    await app.close();
    console.log('Seed run finished.');
}
main().catch((err) => {
    console.error(err);
    process.exit(1);
});
//# sourceMappingURL=run-seed-once.js.map