import { Injectable, Logger, OnModuleInit } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import * as bcrypt from 'bcryptjs';
import { Model } from 'mongoose';
import { SEED_ROLE_DEFINITIONS } from './constants';
import { AuthSettingsService } from './auth-settings.service';
import { RoleDefinition, RoleDefinitionDocument } from './schemas/role-definition.schema';
import { User, UserDocument } from './schemas/user.schema';
import { Store, StoreDocument } from '../stores/schemas/store.schema';

const SEED_ADMIN_EMAIL = 'admin@gmail.com';
const SEED_ADMIN_PASSWORD = '123456';
const SEED_ADMIN_NAME = 'Admin';

const SEED_STORES = [
  { code: 'store-001', name: 'RR Bridal - Main', address: 'Anna Nagar, Chennai', phone: '044-2600-1001' },
  { code: 'store-002', name: 'RR Bridal - T Nagar', address: 'T Nagar, Chennai', phone: '044-2600-1002' },
  { code: 'store-003', name: 'RR Bridal - Velachery', address: 'Velachery, Chennai', phone: '044-2600-1003' },
];

const SEED_STORE_USERS = [
  { email: 'ravi@rrbridal.com', password: '123456', name: 'Ravi Kumar', role: 'store', storeId: 'store-001' },
  { email: 'priya@rrbridal.com', password: '123456', name: 'Priya Sharma', role: 'store', storeId: 'store-001' },
  { email: 'anand@rrbridal.com', password: '123456', name: 'Anand Raj', role: 'store', storeId: 'store-001' },
  { email: 'deepa@rrbridal.com', password: '123456', name: 'Deepa Lakshmi', role: 'store', storeId: 'store-002' },
  { email: 'vijay@rrbridal.com', password: '123456', name: 'Vijay Mohan', role: 'store', storeId: 'store-002' },
  { email: 'kavitha@rrbridal.com', password: '123456', name: 'Kavitha Devi', role: 'store', storeId: 'store-003' },
  { email: 'suresh@rrbridal.com', password: '123456', name: 'Suresh Babu', role: 'store', storeId: 'store-003' },
];

@Injectable()
export class UsersSeedService implements OnModuleInit {
  private readonly logger = new Logger(UsersSeedService.name);

  constructor(
    @InjectModel(RoleDefinition.name) private readonly roleModel: Model<RoleDefinitionDocument>,
    @InjectModel(User.name) private readonly userModel: Model<UserDocument>,
    @InjectModel(Store.name) private readonly storeModel: Model<StoreDocument>,
    private readonly authSettingsService: AuthSettingsService,
  ) {}

  async onModuleInit() {
    await this.seedRoles();
    await this.authSettingsService.ensureDefault();
    await this.seedStores();
    await this.seedAdmin();
    await this.seedStoreUsers();
  }

  private async seedRoles() {
    for (const r of SEED_ROLE_DEFINITIONS) {
      await this.roleModel.updateOne(
        { code: r.code },
        { $setOnInsert: { code: r.code }, $set: { displayName: r.displayName, description: r.description, sortOrder: r.sortOrder } },
        { upsert: true },
      );
    }
  }

  private async seedAdmin() {
    const existing = await this.userModel.findOne({ email: SEED_ADMIN_EMAIL }).lean();
    if (existing) {
      this.logger.log(`Admin user (${SEED_ADMIN_EMAIL}) already exists — skipping seed`);
      return;
    }
    const passwordHash = await bcrypt.hash(SEED_ADMIN_PASSWORD, 10);
    await this.userModel.create({
      email: SEED_ADMIN_EMAIL,
      passwordHash,
      name: SEED_ADMIN_NAME,
      role: 'admin',
      locationKind: 'all',
      status: 'active',
    });
    this.logger.log(`Seeded admin user: ${SEED_ADMIN_EMAIL}`);
  }

  private async seedStores() {
    for (const store of SEED_STORES) {
      const existing = await this.storeModel.findOne({ code: store.code }).lean();
      if (existing) {
        this.logger.log(`Store (${store.code}) already exists — skipping`);
        continue;
      }
      await this.storeModel.create({
        code: store.code,
        name: store.name,
        address: store.address,
        phone: store.phone,
        status: 'active',
      });
      this.logger.log(`Seeded store: ${store.code} — ${store.name}`);
    }
  }

  private async seedStoreUsers() {
    for (const u of SEED_STORE_USERS) {
      const existing = await this.userModel.findOne({ email: u.email }).lean();
      if (existing) {
        this.logger.log(`Store user (${u.email}) already exists — skipping`);
        continue;
      }
      const passwordHash = await bcrypt.hash(u.password, 10);
      await this.userModel.create({
        email: u.email,
        passwordHash,
        name: u.name,
        role: u.role,
        locationKind: 'store',
        storeId: u.storeId,
        status: 'active',
      });
      this.logger.log(`Seeded store user: ${u.email} (${u.storeId})`);
    }
  }
}
