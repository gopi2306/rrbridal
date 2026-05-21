import { Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model, SortOrder } from 'mongoose';
import { CreateRoleAccessDto } from './dto/create-role-access.dto';
import { FilterRoleAccessDto } from './dto/filter-role-access.dto';
import { RoleAccessPermissionDto } from './dto/role-access-permission.dto';
import { UpdateRoleAccessDto } from './dto/update-role-access.dto';
import { RoleAccess, RoleAccessDocument } from './schemas/role-access.schema';

@Injectable()
export class RoleAccessService {
  constructor(@InjectModel(RoleAccess.name) private readonly model: Model<RoleAccessDocument>) {}

  private normalizeRole(role: string): string {
    return role.trim().toLowerCase();
  }

  private normalizeArea(area: string): string {
    return area.trim().toLowerCase();
  }

  private normalizeScreen(screen: string): string {
    return screen.trim();
  }

  async create(dto: CreateRoleAccessDto) {
    const role = this.normalizeRole(dto.role);
    const area = this.normalizeArea(dto.area);
    const screen = this.normalizeScreen(dto.screen);

    const doc = await this.model.findOneAndUpdate(
      { role, area, screen },
      {
        $set: {
          role,
          area,
          screen,
          allow: dto.allow ?? false,
          status: dto.status ?? 'active',
        },
      },
      { upsert: true, new: true },
    );
    return doc.toObject();
  }

  async findById(id: string) {
    const doc = await this.model.findById(id).lean();
    if (!doc) throw new NotFoundException('Role access not found');
    return doc;
  }

  async update(id: string, dto: UpdateRoleAccessDto) {
    const set: Record<string, unknown> = {};
    if (dto.allow !== undefined) set.allow = dto.allow;
    if (dto.status !== undefined) set.status = dto.status;
    const doc = await this.model.findByIdAndUpdate(id, { $set: set }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Role access not found');
    return doc;
  }

  async remove(id: string) {
    const doc = await this.model
      .findByIdAndUpdate(id, { $set: { status: 'inactive', allow: false } }, { new: true })
      .lean();
    if (!doc) throw new NotFoundException('Role access not found');
    return doc;
  }

  async filter(dto: FilterRoleAccessDto) {
    const filter: FilterQuery<RoleAccessDocument> = {};

    if (dto.role) filter.role = this.normalizeRole(dto.role);
    if (dto.area) filter.area = this.normalizeArea(dto.area);
    if (dto.screen) filter.screen = this.normalizeScreen(dto.screen);
    if (dto.allow !== undefined && dto.allow !== null) filter.allow = dto.allow;
    if (dto.status) filter.status = dto.status;

    const page = dto.page ?? 1;
    const limit = dto.limit ?? 500;
    const skip = (page - 1) * limit;
    const sortBy = dto.sortBy ?? 'area';
    const sortOrder: SortOrder = dto.sortOrder === 'desc' ? -1 : 1;

    const [data, total] = await Promise.all([
      this.model
        .find(filter)
        .sort({ [sortBy]: sortOrder, screen: 1 })
        .skip(skip)
        .limit(limit)
        .lean(),
      this.model.countDocuments(filter),
    ]);

    return { data, total, page, limit, totalPages: Math.ceil(total / limit) || 1 };
  }

  async listForRole(role: string) {
    const normalized = this.normalizeRole(role);
    const result = await this.filter({ role: normalized, status: 'active', limit: 500 });
    return { role: normalized, rows: result.data };
  }

  async upsertByRole(role: string, permissions: RoleAccessPermissionDto[]) {
    const normalized = this.normalizeRole(role);
    const ops = permissions.map((p) => {
      const area = this.normalizeArea(p.area);
      const screen = this.normalizeScreen(p.screen);
      return {
        updateOne: {
          filter: { role: normalized, area, screen },
          update: {
            $set: {
              role: normalized,
              area,
              screen,
              allow: p.allow,
              status: 'active' as const,
            },
          },
          upsert: true,
        },
      };
    });
    if (ops.length) await this.model.bulkWrite(ops);
    return await this.listForRole(normalized);
  }

  async allowAllScreensForRole(role: string) {
    const normalized = this.normalizeRole(role);
    await this.model.updateMany(
      { role: normalized, status: 'active' },
      { $set: { allow: true } },
    );
    return await this.listForRole(normalized);
  }
}
