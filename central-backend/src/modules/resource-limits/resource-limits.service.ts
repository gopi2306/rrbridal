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
  stores: {
    limit: number;
    current: number;
    maxUsersPerStore: number;
    usersByStore: Array<{ storeId: string; current: number; limit: number }>;
  };
  warehouses: {
    limit: number;
    current: number;
    maxUsersPerWarehouse: number;
    usersByWarehouse: Array<{ warehouseLocationCode: string; current: number; limit: number }>;
  };
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

    const [activeStores, usersByStore, usersByWarehouse] = await Promise.all([
      this.storeModel.countDocuments({ status: 'active' }),
      this.getUsersByStore(limits.maxUsersPerStore),
      this.getUsersByWarehouse(limits.maxUsersPerWarehouse),
    ]);

    return {
      users,
      stores: {
        limit: limits.maxStores,
        current: activeStores,
        maxUsersPerStore: limits.maxUsersPerStore,
        usersByStore,
      },
      warehouses: {
        limit: limits.maxWarehouses,
        current: activeWarehouseLocations,
        maxUsersPerWarehouse: limits.maxUsersPerWarehouse,
        usersByWarehouse,
      },
    };
  }

  private async getUsersByStore(
    limit: number,
  ): Promise<Array<{ storeId: string; current: number; limit: number }>> {
    const [activeStores, rows] = await Promise.all([
      this.storeModel.find({ status: 'active' }).select('code').lean(),
      this.userModel.aggregate<{ _id: string; count: number }>([
        {
          $match: {
            role: 'store',
            locationKind: 'store',
            status: { $in: ['active', 'invited'] },
            storeId: { $exists: true, $nin: [null, ''] },
          },
        },
        { $group: { _id: '$storeId', count: { $sum: 1 } } },
      ]),
    ]);
    const countByStore = new Map(rows.map((r) => [String(r._id).toLowerCase(), r.count]));
    const storeIds = new Set<string>();
    for (const s of activeStores) storeIds.add(String(s.code).toLowerCase());
    for (const id of countByStore.keys()) storeIds.add(id);
    return [...storeIds].sort().map((storeId) => ({
      storeId,
      current: countByStore.get(storeId) ?? 0,
      limit,
    }));
  }

  private async getUsersByWarehouse(
    limit: number,
  ): Promise<Array<{ warehouseLocationCode: string; current: number; limit: number }>> {
    const [activeWarehouses, rows] = await Promise.all([
      this.locationModel
        .find({ type: WAREHOUSE_LOCATION_TYPE, isActive: true })
        .select('code')
        .lean(),
      this.userModel.aggregate<{ _id: string; count: number }>([
        {
          $match: {
            role: 'warehouse',
            locationKind: 'warehouse',
            status: { $in: ['active', 'invited'] },
            warehouseLocationCode: { $exists: true, $nin: [null, ''] },
          },
        },
        { $group: { _id: '$warehouseLocationCode', count: { $sum: 1 } } },
      ]),
    ]);
    const countBySite = new Map(rows.map((r) => [String(r._id).toLowerCase(), r.count]));
    const siteCodes = new Set<string>();
    for (const loc of activeWarehouses) siteCodes.add(String(loc.code).toLowerCase());
    for (const code of countBySite.keys()) siteCodes.add(code);
    return [...siteCodes].sort().map((warehouseLocationCode) => ({
      warehouseLocationCode,
      current: countBySite.get(warehouseLocationCode) ?? 0,
      limit,
    }));
  }

  private async peakUsersPerStore(): Promise<{ storeId: string; count: number } | null> {
    const rows = await this.getUsersByStore(
      (await this.ensureDefaults()).maxUsersPerStore,
    );
    if (!rows.length) return null;
    const top = rows.reduce((a, b) => (b.current > a.current ? b : a));
    if (top.current === 0) return null;
    return { storeId: top.storeId, count: top.current };
  }

  private async peakUsersPerWarehouse(): Promise<{ warehouseLocationCode: string; count: number } | null> {
    const rows = await this.getUsersByWarehouse(
      (await this.ensureDefaults()).maxUsersPerWarehouse,
    );
    if (!rows.length) return null;
    const top = rows.reduce((a, b) => (b.current > a.current ? b : a));
    if (top.current === 0) return null;
    return { warehouseLocationCode: top.warehouseLocationCode, count: top.current };
  }

  private async validatePatchAgainstUsage(dto: PatchResourceLimitsDto): Promise<void> {
    if (dto.stores != null) {
      const activeStores = await this.storeModel.countDocuments({ status: 'active' });
      if (dto.stores < activeStores) {
        throw new BadRequestException(
          `Cannot set stores limit to ${dto.stores}; ${activeStores} active stores already exist`,
        );
      }
    }

    if (dto.warehouses != null) {
      const activeWarehouses = await this.countActiveWarehouseLocations();
      if (dto.warehouses < activeWarehouses) {
        throw new BadRequestException(
          `Cannot set warehouses limit to ${dto.warehouses}; ${activeWarehouses} active warehouse locations already exist`,
        );
      }
    }

    if (dto.maxUsersPerStore != null) {
      const peak = await this.peakUsersPerStore();
      if (peak && dto.maxUsersPerStore < peak.count) {
        throw new BadRequestException(
          `Cannot set maxUsersPerStore to ${dto.maxUsersPerStore}; store '${peak.storeId}' already has ${peak.count} active or invited users`,
        );
      }
    }

    if (dto.maxUsersPerWarehouse != null) {
      const peak = await this.peakUsersPerWarehouse();
      if (peak && dto.maxUsersPerWarehouse < peak.count) {
        throw new BadRequestException(
          `Cannot set maxUsersPerWarehouse to ${dto.maxUsersPerWarehouse}; warehouse '${peak.warehouseLocationCode}' already has ${peak.count} active or invited users`,
        );
      }
    }
  }

  async patch(dto: PatchResourceLimitsDto): Promise<ResourceLimitUsage> {
    const doc = await this.ensureDefaults();
    await this.validatePatchAgainstUsage(dto);

    if (dto.stores != null) {
      doc.maxStores = dto.stores;
    }
    if (dto.warehouses != null) {
      doc.maxWarehouses = dto.warehouses;
    }
    if (dto.maxUsersPerStore != null) {
      doc.maxUsersPerStore = dto.maxUsersPerStore;
    }
    if (dto.maxUsersPerWarehouse != null) {
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

  /** Call when reactivating a store (excludes `excludeStoreCode` from the active count). */
  async assertStoreLimitForActivation(excludeStoreCode?: string): Promise<void> {
    const doc = await this.ensureDefaults();
    const filter: Record<string, unknown> = { status: 'active' };
    if (excludeStoreCode) {
      filter.code = { $ne: excludeStoreCode.trim().toLowerCase() };
    }
    const others = await this.storeModel.countDocuments(filter);
    if (others >= doc.maxStores) {
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
      locationKind: 'store',
      storeId: sid,
      status: { $in: ['active', 'invited'] },
    };
    if (excludeUserId && Types.ObjectId.isValid(excludeUserId)) {
      filter._id = { $ne: new Types.ObjectId(excludeUserId) };
    }
    const n = await this.userModel.countDocuments(filter);
    if (n >= doc.maxUsersPerStore) {
      throw new BadRequestException(
        `Store '${sid}' has reached the maximum of ${doc.maxUsersPerStore} users. Contact admin to increase the limit.`,
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
        `Warehouse '${c}' has reached the maximum of ${doc.maxUsersPerWarehouse} users. Contact admin to increase the limit.`,
      );
    }
  }
}
