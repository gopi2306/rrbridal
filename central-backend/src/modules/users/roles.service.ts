import {
  BadRequestException,
  ConflictException,
  Injectable,
  NotFoundException,
} from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model } from 'mongoose';
import { isValidObjectIdString } from '../../common/object-id.util';
import { CreateRoleDefinitionDto } from './dto/create-role-definition.dto';
import { UpdateRoleDefinitionDto } from './dto/update-role-definition.dto';
import { RoleDefinition, RoleDefinitionDocument } from './schemas/role-definition.schema';

const PROTECTED_ROLE_CODES = new Set(['super_admin']);

@Injectable()
export class RolesService {
  constructor(
    @InjectModel(RoleDefinition.name) private readonly roleModel: Model<RoleDefinitionDocument>,
  ) {}

  private notDeletedFilter(): FilterQuery<RoleDefinitionDocument> {
    return { deletedAt: { $exists: false } };
  }

  private normalizeCode(code: string): string {
    return code.trim().toLowerCase();
  }

  async create(dto: CreateRoleDefinitionDto) {
    const code = this.normalizeCode(dto.code);
    if (PROTECTED_ROLE_CODES.has(code)) {
      throw new BadRequestException(`Role code '${code}' is reserved`);
    }
    const existing = await this.roleModel.findOne({ code }).lean();
    if (existing && !existing.deletedAt) {
      throw new ConflictException(`Role code '${code}' already exists`);
    }
    if (existing?.deletedAt) {
      const doc = await this.roleModel
        .findByIdAndUpdate(
          existing._id,
          {
            $set: {
              displayName: dto.displayName.trim(),
              description: dto.description?.trim(),
              sortOrder: dto.sortOrder ?? 0,
              isActive: dto.isActive ?? true,
            },
            $unset: { deletedAt: '' },
          },
          { new: true },
        )
        .lean();
      return doc;
    }
    return await this.roleModel.create({
      code,
      displayName: dto.displayName.trim(),
      description: dto.description?.trim(),
      sortOrder: dto.sortOrder ?? 0,
      isActive: dto.isActive ?? true,
    });
  }

  async list(includeInactive = false) {
    const filter: FilterQuery<RoleDefinitionDocument> = this.notDeletedFilter();
    if (!includeInactive) {
      filter.isActive = { $ne: false }; // treats missing isActive as active (legacy rows)
    }
    return await this.roleModel.find(filter).sort({ sortOrder: 1, displayName: 1 }).lean();
  }

  async findById(id: string) {
    if (!isValidObjectIdString(id)) throw new NotFoundException('Role not found');
    const doc = await this.roleModel.findOne({ _id: id, ...this.notDeletedFilter() }).lean();
    if (!doc) throw new NotFoundException('Role not found');
    return doc;
  }

  async findByCode(code: string) {
    const normalized = this.normalizeCode(code);
    const doc = await this.roleModel.findOne({ code: normalized, ...this.notDeletedFilter() }).lean();
    if (!doc) throw new NotFoundException(`Role not found: '${normalized}'`);
    return doc;
  }

  async update(id: string, dto: UpdateRoleDefinitionDto) {
    if (!isValidObjectIdString(id)) throw new NotFoundException('Role not found');
    const existing = await this.roleModel.findOne({ _id: id, ...this.notDeletedFilter() }).lean();
    if (!existing) throw new NotFoundException('Role not found');

    const set: Record<string, unknown> = {};
    if (dto.displayName !== undefined) set.displayName = dto.displayName.trim();
    if (dto.description !== undefined) set.description = dto.description.trim() || undefined;
    if (dto.sortOrder !== undefined) set.sortOrder = dto.sortOrder;
    if (dto.isActive !== undefined) set.isActive = dto.isActive;

    const doc = await this.roleModel.findByIdAndUpdate(id, { $set: set }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Role not found');
    return doc;
  }

  async softDelete(id: string) {
    if (!isValidObjectIdString(id)) throw new NotFoundException('Role not found');
    const existing = await this.roleModel.findOne({ _id: id, ...this.notDeletedFilter() }).lean();
    if (!existing) throw new NotFoundException('Role not found');
    if (PROTECTED_ROLE_CODES.has(existing.code)) {
      throw new BadRequestException(`Role '${existing.code}' cannot be deleted`);
    }

    const doc = await this.roleModel
      .findByIdAndUpdate(
        id,
        { $set: { isActive: false, deletedAt: new Date() } },
        { new: true },
      )
      .lean();
    if (!doc) throw new NotFoundException('Role not found');
    return doc;
  }
}
