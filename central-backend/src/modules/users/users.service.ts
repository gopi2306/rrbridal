import { BadRequestException, ConflictException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import * as bcrypt from 'bcryptjs';
import { Model, Types } from 'mongoose';
import { StoresService } from '../stores/stores.service';
import { AuthSettingsService } from './auth-settings.service';
import { BootstrapAdminDto } from './dto/bootstrap-admin.dto';
import { CreateUserDto } from './dto/create-user.dto';
import { UpdateUserDto } from './dto/update-user.dto';
import { User, UserDocument, UserRole, UserStatus } from './schemas/user.schema';

type PublicUser = Omit<User, 'passwordHash'> & { _id: Types.ObjectId };

@Injectable()
export class UsersService {
  constructor(
    @InjectModel(User.name) private readonly userModel: Model<UserDocument>,
    private readonly authSettingsService: AuthSettingsService,
    private readonly storesService: StoresService,
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
    await this.assertStoreLocation(dto.locationKind, dto.storeId);
    await this.assertCanAssignRole(dto.role as UserRole);
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
      status: dto.status ?? 'active',
    };
    if (dto.locationKind === 'store' && dto.storeId) row.storeId = dto.storeId.trim();
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
    const nextLocation = dto.locationKind ?? prev.locationKind;
    const nextStoreId = dto.storeId !== undefined ? dto.storeId : prev.storeId;

    await this.assertStoreLocation(nextLocation, nextStoreId);

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
    }

    const set: Record<string, unknown> = {};
    if (dto.name !== undefined) set.name = dto.name.trim();
    if (dto.role !== undefined) set.role = dto.role;
    if (dto.locationKind !== undefined) set.locationKind = dto.locationKind;
    if (dto.status !== undefined) set.status = dto.status;
    if (dto.password !== undefined) set.passwordHash = await bcrypt.hash(dto.password, 10);
    if (nextLocation === 'store' && nextStoreId) set.storeId = String(nextStoreId).trim();

    const updateOps: { $set?: Record<string, unknown>; $unset?: Record<string, string> } = {};
    if (Object.keys(set).length > 0) updateOps.$set = set;
    if (nextLocation !== 'store') updateOps.$unset = { storeId: '' };
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
}
