import { BadRequestException, ConflictException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import * as bcrypt from 'bcryptjs';
import { FilterQuery, Model, SortOrder, Types } from 'mongoose';
import { StoresService } from '../stores/stores.service';
import { ResourceLimitsService } from '../resource-limits/resource-limits.service';
import { AuthSettingsService } from './auth-settings.service';
import { BootstrapAdminDto } from './dto/bootstrap-admin.dto';
import { CreateUserDto } from './dto/create-user.dto';
import { FilterUserDto } from './dto/filter-user.dto';
import { UpdateUserDto } from './dto/update-user.dto';
import { User, UserDocument, UserLocationKind, UserRole, UserStatus } from './schemas/user.schema';

type PublicUser = Omit<User, 'passwordHash'> & { _id: Types.ObjectId };

@Injectable()
export class UsersService {
  constructor(
    @InjectModel(User.name) private readonly userModel: Model<UserDocument>,
    private readonly authSettingsService: AuthSettingsService,
    private readonly storesService: StoresService,
    private readonly resourceLimitsService: ResourceLimitsService,
  ) {}

  async countAll(): Promise<number> {
    return await this.userModel.countDocuments();
  }

  async countTowardQuota(role: string): Promise<number> {
    return await this.userModel.countDocuments({
      role,
      status: { $in: ['active', 'invited'] },
    });
  }

  async assertCanAssignRole(
    targetRole: UserRole,
    existingUser?: { role: UserRole; status: UserStatus },
  ): Promise<void> {
    const cap = await this.authSettingsService.getQuotaForRole(targetRole);
    const n = await this.countTowardQuota(targetRole);
    const alreadyInSlot =
      existingUser &&
      existingUser.role === targetRole &&
      (existingUser.status === 'active' || existingUser.status === 'invited');
    if (alreadyInSlot) return;
    if (n >= cap) {
      throw new BadRequestException(`Role quota reached for '${targetRole}' (maximum ${cap})`);
    }
  }

  private async assertWarehouseAssignment(
    role: UserRole,
    locationKind: string,
    warehouseLocationCode: string | undefined,
    status: UserStatus,
  ): Promise<void> {
    if (role !== 'warehouse' || locationKind !== 'warehouse') return;
    const counts = status === 'active' || status === 'invited';
    const c = warehouseLocationCode?.trim();
    if (!c) {
      if (counts) {
        throw new BadRequestException(
          'warehouseLocationCode is required when role is warehouse and locationKind is warehouse',
        );
      }
      return;
    }
    if (counts) {
      await this.resourceLimitsService.assertActiveWarehouseLocationCode(c);
    }
  }

  private async assertPerSiteUserLimits(
    params: {
      role: UserRole;
      locationKind: string;
      storeId?: string;
      warehouseLocationCode?: string;
      status: UserStatus;
      excludeUserId?: string;
    },
  ): Promise<void> {
    const counts = params.status === 'active' || params.status === 'invited';
    if (!counts) return;
    if (params.role === 'store' && params.storeId) {
      await this.resourceLimitsService.assertUsersPerStoreLimit(
        String(params.storeId).trim().toLowerCase(),
        params.excludeUserId,
      );
    }
    if (
      params.role === 'warehouse' &&
      params.locationKind === 'warehouse' &&
      params.warehouseLocationCode
    ) {
      await this.resourceLimitsService.assertUsersPerWarehouseLimit(
        params.warehouseLocationCode,
        params.excludeUserId,
      );
    }
  }

  private async assertStoreLocation(locationKind: string, storeId?: string) {
    if (locationKind === 'store' && (!storeId || !String(storeId).trim())) {
      throw new BadRequestException('storeId is required when locationKind is store');
    }
    if (locationKind === 'store' && storeId) {
      const exists = await this.storesService.existsByCode(storeId);
      if (!exists) throw new BadRequestException(`Unknown storeId '${storeId}'`);
    }
  }

  private toPublic(doc: Record<string, unknown> | null | undefined): PublicUser | null {
    if (!doc) return null;
    const { passwordHash: _p, ...rest } = doc as Record<string, unknown> & { passwordHash?: string };
    return rest as unknown as PublicUser;
  }

  async findForLogin(email: string) {
    const normalized = email.trim().toLowerCase();
    return await this.userModel.findOne({ email: normalized }).select('+passwordHash').lean();
  }

  async createBootstrapAdmin(dto: BootstrapAdminDto): Promise<PublicUser> {
    const normalized = dto.email.trim().toLowerCase();
    const existing = await this.userModel.findOne({ email: normalized }).lean();
    if (existing) throw new ConflictException('User already exists');
    const passwordHash = await bcrypt.hash(dto.password, 10);
    const created = await this.userModel.create({
      email: normalized,
      passwordHash,
      name: dto.name.trim(),
      role: 'admin' as const,
      locationKind: 'all' as const,
      status: 'active' as const,
    });
    return this.toPublic(created.toObject() as unknown as Record<string, unknown>)!;
  }

  async create(dto: CreateUserDto): Promise<PublicUser> {
    const status = (dto.status ?? 'active') as UserStatus;
    await this.assertStoreLocation(dto.locationKind, dto.storeId);
    await this.assertWarehouseAssignment(
      dto.role as UserRole,
      dto.locationKind,
      dto.warehouseLocationCode,
      status,
    );
    await this.assertCanAssignRole(dto.role as UserRole);
    await this.assertPerSiteUserLimits({
      role: dto.role as UserRole,
      locationKind: dto.locationKind,
      status,
      ...(dto.storeId?.trim() ? { storeId: dto.storeId.trim().toLowerCase() } : {}),
      ...(dto.warehouseLocationCode?.trim()
        ? { warehouseLocationCode: dto.warehouseLocationCode.trim().toLowerCase() }
        : {}),
    });
    const normalized = dto.email.trim().toLowerCase();
    const existing = await this.userModel.findOne({ email: normalized }).lean();
    if (existing) throw new ConflictException('Email already registered');
    const passwordHash = await bcrypt.hash(dto.password, 10);
    const row: Record<string, unknown> = {
      email: normalized,
      passwordHash,
      name: dto.name.trim(),
      role: dto.role,
      locationKind: dto.locationKind,
      status,
    };
    if (dto.locationKind === 'store' && dto.storeId) row.storeId = dto.storeId.trim().toLowerCase();
    if (dto.role === 'warehouse' && dto.locationKind === 'warehouse' && dto.warehouseLocationCode) {
      row.warehouseLocationCode = dto.warehouseLocationCode.trim().toLowerCase();
    }
    const created = await this.userModel.create(row);
    return this.toPublic(created.toObject() as unknown as Record<string, unknown>)!;
  }

  async findById(id: string): Promise<PublicUser> {
    if (!Types.ObjectId.isValid(id)) throw new NotFoundException('User not found');
    const doc = await this.userModel.findById(id).lean();
    if (!doc) throw new NotFoundException('User not found');
    return this.toPublic(doc as Record<string, unknown>)!;
  }

  async list(): Promise<PublicUser[]> {
    const docs = await this.userModel.find().sort({ createdAt: -1 }).limit(500).lean();
    return docs.map((d) => this.toPublic(d as Record<string, unknown>)!);
  }

  async update(id: string, dto: UpdateUserDto): Promise<PublicUser> {
    if (!Types.ObjectId.isValid(id)) throw new NotFoundException('User not found');
    const prev = await this.userModel.findById(id).lean();
    if (!prev) throw new NotFoundException('User not found');

    const nextRole = (dto.role ?? prev.role) as UserRole;
    const nextStatus = (dto.status ?? prev.status) as UserStatus;
    const nextLocation = (dto.locationKind ?? prev.locationKind) as UserLocationKind;
    const nextStoreId = dto.storeId !== undefined ? dto.storeId : prev.storeId;
    const prevWarehouse = (prev as { warehouseLocationCode?: string }).warehouseLocationCode;
    const nextWarehouseLocationCode =
      dto.warehouseLocationCode !== undefined
        ? String(dto.warehouseLocationCode).trim() === ''
          ? undefined
          : String(dto.warehouseLocationCode).trim().toLowerCase()
        : prevWarehouse;

    await this.assertStoreLocation(nextLocation, nextStoreId);
    await this.assertWarehouseAssignment(nextRole, nextLocation, nextWarehouseLocationCode, nextStatus);

    const prevCounts = prev.status === 'active' || prev.status === 'invited';
    const nextCounts = nextStatus === 'active' || nextStatus === 'invited';
    if (nextCounts) {
      const sameSlot =
        prevCounts && prev.role === nextRole && (nextStatus === 'active' || nextStatus === 'invited');
      if (sameSlot) {
        await this.assertCanAssignRole(nextRole, { role: prev.role as UserRole, status: prev.status as UserStatus });
      } else {
        await this.assertCanAssignRole(nextRole);
      }
      await this.assertPerSiteUserLimits({
        role: nextRole,
        locationKind: nextLocation,
        status: nextStatus,
        excludeUserId: id,
        ...(nextStoreId ? { storeId: String(nextStoreId).trim().toLowerCase() } : {}),
        ...(nextWarehouseLocationCode ? { warehouseLocationCode: nextWarehouseLocationCode } : {}),
      });
    }

    const set: Record<string, unknown> = {};
    if (dto.name !== undefined) set.name = dto.name.trim();
    if (dto.role !== undefined) set.role = dto.role;
    if (dto.locationKind !== undefined) set.locationKind = dto.locationKind;
    if (dto.status !== undefined) set.status = dto.status;
    if (dto.password !== undefined) set.passwordHash = await bcrypt.hash(dto.password, 10);
    if (nextLocation === 'store' && nextStoreId) set.storeId = String(nextStoreId).trim().toLowerCase();
    if (nextRole === 'warehouse' && nextLocation === 'warehouse' && nextWarehouseLocationCode) {
      set.warehouseLocationCode = nextWarehouseLocationCode;
    }

    const unset: Record<string, string> = {};
    if (nextLocation !== 'store') unset.storeId = '';
    if (!(nextRole === 'warehouse' && nextLocation === 'warehouse')) unset.warehouseLocationCode = '';

    const updateOps: { $set?: Record<string, unknown>; $unset?: Record<string, string> } = {};
    if (Object.keys(set).length > 0) updateOps.$set = set;
    if (Object.keys(unset).length > 0) updateOps.$unset = unset;
    if (!updateOps.$set && !updateOps.$unset) {
      return this.toPublic(prev as Record<string, unknown>)!;
    }

    const updated = await this.userModel.findByIdAndUpdate(id, updateOps, { new: true }).lean();
    if (!updated) throw new NotFoundException('User not found');
    return this.toPublic(updated as Record<string, unknown>)!;
  }

  async listByStoreWithHash(storeId: string) {
    return await this.userModel
      .find({ storeId, status: 'active' })
      .select('+passwordHash')
      .sort({ name: 1 })
      .lean();
  }

  async disable(id: string): Promise<PublicUser> {
    return await this.update(id, { status: 'disabled' });
  }

  async filter(dto: FilterUserDto) {
    const filter: FilterQuery<UserDocument> = {};

    if (dto.email) filter.email = dto.email.trim().toLowerCase();
    if (dto.name) filter.name = dto.name;
    if (dto.role) filter.role = dto.role;
    if (dto.locationKind) filter.locationKind = dto.locationKind;
    if (dto.storeId) filter.storeId = dto.storeId.trim().toLowerCase();
    if (dto.warehouseLocationCode) filter.warehouseLocationCode = dto.warehouseLocationCode.trim().toLowerCase();
    if (dto.status) filter.status = dto.status;

    if (dto.search) {
      filter.$or = [
        { name: { $regex: dto.search, $options: 'i' } },
        { email: { $regex: dto.search, $options: 'i' } },
      ];
    }

    const page = dto.page ?? 1;
    const limit = dto.limit ?? 20;
    const skip = (page - 1) * limit;
    const sortBy = dto.sortBy ?? 'createdAt';
    const sortOrder: SortOrder = dto.sortOrder === 'asc' ? 1 : -1;

    const [docs, total] = await Promise.all([
      this.userModel
        .find(filter)
        .sort({ [sortBy]: sortOrder })
        .skip(skip)
        .limit(limit)
        .lean(),
      this.userModel.countDocuments(filter),
    ]);

    return {
      data: docs.map((d) => this.toPublic(d as Record<string, unknown>)!),
      total,
      page,
      limit,
      totalPages: Math.ceil(total / limit),
    };
  }
}
