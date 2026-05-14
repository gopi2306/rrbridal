import { BadRequestException, Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model, Types } from 'mongoose';
import { AuthSettingsService } from '../users/auth-settings.service';
import { User, UserDocument } from '../users/schemas/user.schema';
import { Store, StoreDocument } from '../stores/schemas/store.schema';
import { Location, LocationDocument } from '../locations/schemas/location.schema';
import { ResourceLimits, ResourceLimitsDocument, RESOURCE_LIMITS_KEY } from './schemas/resource-limits.schema';
import { PatchResourceLimitsDto } from './dto/patch-resource-limits.dto';

/** Normalized `Location.type` value for warehouse sites (enforced against `maxWarehouses`). */
export const WAREHOUSE_LOCATION_TYPE = 'warehouse';

export interface ResourceLimitUsage {
  users: Record<string, { limit: number; current: number }>;
  stores: { limit: number; current: number; maxUsersPerStore: number };
  warehouses: { limit: number; current: number; maxUsersPerWarehouse: number };
}

@Injectable()
export class ResourceLimitsService {
  constructor(
    @InjectModel(ResourceLimits.name) private readonly limitsModel: Model<ResourceLimitsDocument>,
    @InjectModel(User.name) private readonly userModel: Model<UserDocument>,
    @InjectModel(Store.name) private readonly storeModel: Model<StoreDocument>,
    @InjectModel(Location.name) private readonly locationModel: Model<LocationDocument>,
    private readonly authSettingsService: AuthSettingsService,
  ) {}

  private async ensureDefaults(): Promise<ResourceLimitsDocument> {
    let doc = await this.limitsModel.findOne({ settingsKey: RESOURCE_LIMITS_KEY });
    if (!doc) {
      doc = await this.limitsModel.create({
        settingsKey: RESOURCE_LIMITS_KEY,
        maxStores: 3,
        maxWarehouses: 5,
        maxUsersPerStore: 20,
        maxUsersPerWarehouse: 20,
      });
    } else {
      let changed = false;
      if (doc.maxUsersPerStore === undefined || doc.maxUsersPerStore === null) {
        doc.maxUsersPerStore = 20;
        changed = true;
      }
      if (doc.maxUsersPerWarehouse === undefined || doc.maxUsersPerWarehouse === null) {
        doc.maxUsersPerWarehouse = 20;
        changed = true;
      }
      if (changed) await doc.save();
    }
    return doc;
  }

  private countActiveWarehouseLocations(): Promise<number> {
    return this.locationModel.countDocuments({
      type: WAREHOUSE_LOCATION_TYPE,
      isActive: true,
    });
  }

  async getUsage(): Promise<ResourceLimitUsage> {
    const [limits, roleQuotas, activeWarehouseLocations] = await Promise.all([
      this.ensureDefaults(),
      this.authSettingsService.getQuotas(),
      this.countActiveWarehouseLocations(),
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

    return {
      users,
      stores: {
        limit: limits.maxStores,
        current: activeStores,
        maxUsersPerStore: limits.maxUsersPerStore,
      },
      warehouses: {
        limit: limits.maxWarehouses,
        current: activeWarehouseLocations,
        maxUsersPerWarehouse: limits.maxUsersPerWarehouse,
      },
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
    if (dto.maxUsersPerStore !== undefined) {
      doc.maxUsersPerStore = dto.maxUsersPerStore;
    }
    if (dto.maxUsersPerWarehouse !== undefined) {
      doc.maxUsersPerWarehouse = dto.maxUsersPerWarehouse;
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

  async assertWarehouseLimit(): Promise<void> {
    const doc = await this.ensureDefaults();
    const n = await this.countActiveWarehouseLocations();
    if (n >= doc.maxWarehouses) {
      throw new BadRequestException(
        `Warehouse location limit reached (maximum ${doc.maxWarehouses}). Contact admin to increase the limit.`,
      );
    }
  }

  /** Call when activating or creating an active warehouse location (excludes `excludeLocationId` from the count). */
  async assertWarehouseLimitForActivation(excludeLocationId?: string): Promise<void> {
    const doc = await this.ensureDefaults();
    const filter: Record<string, unknown> = { type: WAREHOUSE_LOCATION_TYPE, isActive: true };
    if (excludeLocationId) {
      filter._id = { $ne: excludeLocationId };
    }
    const others = await this.locationModel.countDocuments(filter);
    if (others >= doc.maxWarehouses) {
      throw new BadRequestException(
        `Warehouse location limit reached (maximum ${doc.maxWarehouses}). Contact admin to increase the limit.`,
      );
    }
  }

  /** Ensures `code` matches an active warehouse-type location. */
  async assertActiveWarehouseLocationCode(code: string): Promise<void> {
    const c = code.trim().toLowerCase();
    const loc = await this.locationModel
      .findOne({ code: c, type: WAREHOUSE_LOCATION_TYPE, isActive: true })
      .lean();
    if (!loc) {
      throw new BadRequestException(`Unknown or inactive warehouse location code '${c}'`);
    }
  }

  /** Counts active/invited store-role users for this store (excluding one user id when updating). */
  async assertUsersPerStoreLimit(storeId: string, excludeUserId?: string): Promise<void> {
    const doc = await this.ensureDefaults();
    const sid = storeId.trim().toLowerCase();
    const filter: Record<string, unknown> = {
      role: 'store',
      storeId: sid,
      status: { $in: ['active', 'invited'] },
    };
    if (excludeUserId && Types.ObjectId.isValid(excludeUserId)) {
      filter._id = { $ne: new Types.ObjectId(excludeUserId) };
    }
    const n = await this.userModel.countDocuments(filter);
    if (n >= doc.maxUsersPerStore) {
      throw new BadRequestException(
        `Users per store limit reached for this store (maximum ${doc.maxUsersPerStore}). Contact admin to increase the limit.`,
      );
    }
  }

  /** Counts active/invited warehouse-scoped users for this warehouse code (excluding one user id when updating). */
  async assertUsersPerWarehouseLimit(warehouseLocationCode: string, excludeUserId?: string): Promise<void> {
    const doc = await this.ensureDefaults();
    const c = warehouseLocationCode.trim().toLowerCase();
    const filter: Record<string, unknown> = {
      role: 'warehouse',
      locationKind: 'warehouse',
      warehouseLocationCode: c,
      status: { $in: ['active', 'invited'] },
    };
    if (excludeUserId && Types.ObjectId.isValid(excludeUserId)) {
      filter._id = { $ne: new Types.ObjectId(excludeUserId) };
    }
    const n = await this.userModel.countDocuments(filter);
    if (n >= doc.maxUsersPerWarehouse) {
      throw new BadRequestException(
        `Users per warehouse limit reached for this site (maximum ${doc.maxUsersPerWarehouse}). Contact admin to increase the limit.`,
      );
    }
  }
}
