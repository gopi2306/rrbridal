import { BadRequestException, Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { AuthSettingsService } from '../users/auth-settings.service';
import { User, UserDocument } from '../users/schemas/user.schema';
import { Store, StoreDocument } from '../stores/schemas/store.schema';
import { ResourceLimits, ResourceLimitsDocument, RESOURCE_LIMITS_KEY } from './schemas/resource-limits.schema';
import { PatchResourceLimitsDto } from './dto/patch-resource-limits.dto';

export interface ResourceLimitUsage {
  users: Record<string, { limit: number; current: number }>;
  stores: { limit: number; current: number };
  warehouses: { limit: number; current: number };
}

@Injectable()
export class ResourceLimitsService {
  constructor(
    @InjectModel(ResourceLimits.name) private readonly limitsModel: Model<ResourceLimitsDocument>,
    @InjectModel(User.name) private readonly userModel: Model<UserDocument>,
    @InjectModel(Store.name) private readonly storeModel: Model<StoreDocument>,
    private readonly authSettingsService: AuthSettingsService,
  ) {}

  private async ensureDefaults(): Promise<ResourceLimitsDocument> {
    let doc = await this.limitsModel.findOne({ settingsKey: RESOURCE_LIMITS_KEY });
    if (!doc) {
      doc = await this.limitsModel.create({
        settingsKey: RESOURCE_LIMITS_KEY,
        maxStores: 3,
        maxWarehouses: 1,
      });
    }
    return doc;
  }

  async getUsage(): Promise<ResourceLimitUsage> {
    const [limits, roleQuotas] = await Promise.all([
      this.ensureDefaults(),
      this.authSettingsService.getQuotas(),
    ]);

    const roles = Object.keys(roleQuotas);
    const roleCounts = await Promise.all(
      roles.map(async (role) => {
        const count = await this.userModel.countDocuments({
          role,
          status: { $in: ['active', 'invited'] },
        });
        return { role, count };
      }),
    );

    const users: Record<string, { limit: number; current: number }> = {};
    for (const { role, count } of roleCounts) {
      users[role] = { limit: roleQuotas[role] ?? 99, current: count };
    }

    const activeStores = await this.storeModel.countDocuments({ status: 'active' });
    const warehouseUsers = await this.userModel.countDocuments({
      role: 'warehouse',
      status: { $in: ['active', 'invited'] },
    });

    return {
      users,
      stores: { limit: limits.maxStores, current: activeStores },
      warehouses: { limit: limits.maxWarehouses, current: warehouseUsers },
    };
  }

  async patch(dto: PatchResourceLimitsDto): Promise<ResourceLimitUsage> {
    const doc = await this.ensureDefaults();

    if (dto.stores !== undefined) {
      doc.maxStores = dto.stores;
    }
    if (dto.warehouses !== undefined) {
      doc.maxWarehouses = dto.warehouses;
    }
    await doc.save();

    if (dto.users) {
      const patch: Record<string, number> = {};
      for (const [role, limit] of Object.entries(dto.users)) {
        if (typeof limit === 'number' && Number.isFinite(limit) && limit >= 0) {
          patch[role] = limit;
        }
      }
      if (Object.keys(patch).length > 0) {
        await this.authSettingsService.updateRoleQuotas(patch);
      }

      if (dto.warehouses === undefined && patch['warehouse'] !== undefined) {
        doc.maxWarehouses = patch['warehouse'];
        await doc.save();
      }
    }

    return await this.getUsage();
  }

  async assertStoreLimit(): Promise<void> {
    const doc = await this.ensureDefaults();
    const activeStores = await this.storeModel.countDocuments({ status: 'active' });
    if (activeStores >= doc.maxStores) {
      throw new BadRequestException(
        `Store limit reached (maximum ${doc.maxStores}). Contact admin to increase the limit.`,
      );
    }
  }
}
