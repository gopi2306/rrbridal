import { Injectable, Logger, OnModuleInit } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { Store, StoreDocument } from '../stores/schemas/store.schema';
import { SEED_COMPANY_PROFILE, SEED_STORE_RECEIPT_PRINT_SETTINGS } from './company-profile-seed.constants';
import {
  CompanyProfile,
  CompanyProfileDocument,
  COMPANY_PROFILE_KEY,
} from './schemas/company-profile.schema';

@Injectable()
export class CompanyProfileSeedService implements OnModuleInit {
  private readonly logger = new Logger(CompanyProfileSeedService.name);

  constructor(
    @InjectModel(CompanyProfile.name) private readonly companyModel: Model<CompanyProfileDocument>,
    @InjectModel(Store.name) private readonly storeModel: Model<StoreDocument>,
  ) {}

  async onModuleInit() {
    if (process.env.SEED_SKIP_COMPANY_PROFILE === 'true') {
      this.logger.log('SEED_SKIP_COMPANY_PROFILE set — skipping company profile seed');
      return;
    }

    await this.runSeed();
  }

  /** Idempotent company + store receipt settings seed (callable from CLI). */
  async runSeed() {
    await this.seedCompanyProfile();
    await this.seedStoreReceiptPrintSettings();
  }

  private async seedCompanyProfile() {
    const existing = await this.companyModel.findOne({ settingsKey: COMPANY_PROFILE_KEY }).lean();
    const force = process.env.SEED_FORCE_COMPANY_PROFILE === 'true';
    if (existing?.tradeName?.trim() && !force) {
      this.logger.log('Company profile already configured — skipping company seed (set SEED_FORCE_COMPANY_PROFILE=true to overwrite)');
      return;
    }

    const { settingsKey, ...profile } = SEED_COMPANY_PROFILE;
    await this.companyModel.findOneAndUpdate(
      { settingsKey },
      { $set: { settingsKey, ...profile } },
      { upsert: true, new: true },
    );
    this.logger.log(`Seeded company profile: ${SEED_COMPANY_PROFILE.tradeName}`);
  }

  private async seedStoreReceiptPrintSettings() {
    const stores = await this.storeModel.find().lean();
    if (stores.length === 0) {
      this.logger.log('No stores found — skipping receipt print settings seed');
      return;
    }

    let updated = 0;
    for (const store of stores) {
      if (store.receiptPrintSettings?.receiptCharWidth) continue;
      await this.storeModel.updateOne(
        { _id: store._id },
        { $set: { receiptPrintSettings: { ...SEED_STORE_RECEIPT_PRINT_SETTINGS } } },
      );
      updated += 1;
      this.logger.log(`Seeded receiptPrintSettings for store: ${store.code}`);
    }

    if (updated === 0) {
      this.logger.log('All stores already have receiptPrintSettings — skipping');
    }
  }
}
