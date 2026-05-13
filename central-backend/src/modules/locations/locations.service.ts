import { ConflictException, Inject, Injectable, NotFoundException, forwardRef } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model, SortOrder } from 'mongoose';
import { ResourceLimitsService, WAREHOUSE_LOCATION_TYPE } from '../resource-limits/resource-limits.service';
import { CreateLocationDto } from './dto/create-location.dto';
import { FilterLocationDto } from './dto/filter-location.dto';
import { UpdateLocationDto } from './dto/update-location.dto';
import { Location, LocationDocument } from './schemas/location.schema';

@Injectable()
export class LocationsService {
  constructor(
    @InjectModel(Location.name) private readonly model: Model<LocationDocument>,
    @Inject(forwardRef(() => ResourceLimitsService)) private readonly resourceLimits: ResourceLimitsService,
  ) {}

  private normalizeType(type?: string): string | undefined {
    if (type === undefined || type === null) return undefined;
    const t = type.trim().toLowerCase();
    return t === '' ? undefined : t;
  }

  async create(dto: CreateLocationDto) {
    const code = dto.code.trim().toLowerCase();
    const existing = await this.model.findOne({ code }).lean();
    if (existing) throw new ConflictException(`Code '${code}' already exists`);
    const typeNorm = this.normalizeType(dto.type);
    const isActive = dto.isActive ?? true;
    if (typeNorm === WAREHOUSE_LOCATION_TYPE && isActive) {
      await this.resourceLimits.assertWarehouseLimit();
    }
    return await this.model.create({
      code,
      name: dto.name.trim(),
      address: dto.address?.trim(),
      type: typeNorm,
      isActive,
    });
  }

  async findAll() {
    return await this.model.find().sort({ name: 1 }).lean();
  }

  async filter(dto: FilterLocationDto) {
    const filter: FilterQuery<LocationDocument> = {};

    if (dto.code !== undefined && dto.code !== '') {
      filter.code = dto.code.trim().toLowerCase();
    }

    if (dto.type !== undefined && dto.type !== '') {
      filter.type = dto.type.trim();
    }

    if (dto.isActive !== undefined && dto.isActive !== null) {
      filter.isActive = dto.isActive;
    }

    if (dto.search) {
      filter.$or = [
        { code: { $regex: dto.search, $options: 'i' } },
        { name: { $regex: dto.search, $options: 'i' } },
        { address: { $regex: dto.search, $options: 'i' } },
        { type: { $regex: dto.search, $options: 'i' } },
      ];
    }

    const page = dto.page ?? 1;
    const limit = dto.limit ?? 20;
    const skip = (page - 1) * limit;
    const sortBy = dto.sortBy ?? 'name';
    const sortOrder: SortOrder = dto.sortOrder === 'desc' ? -1 : 1;

    const [data, total] = await Promise.all([
      this.model.find(filter).sort({ [sortBy]: sortOrder }).skip(skip).limit(limit).lean(),
      this.model.countDocuments(filter),
    ]);

    return {
      data,
      total,
      page,
      limit,
      totalPages: Math.ceil(total / limit),
    };
  }

  async findById(id: string) {
    const doc = await this.model.findById(id).lean();
    if (!doc) throw new NotFoundException('Not found');
    return doc;
  }

  async update(id: string, dto: UpdateLocationDto) {
    const prev = await this.model.findById(id).lean();
    if (!prev) throw new NotFoundException('Not found');

    const mergedType = dto.type !== undefined ? this.normalizeType(dto.type) : this.normalizeType(prev.type);
    const mergedActive = dto.isActive !== undefined ? dto.isActive : prev.isActive;
    const wasActiveWarehouse =
      this.normalizeType(prev.type) === WAREHOUSE_LOCATION_TYPE && prev.isActive === true;
    const willBeActiveWarehouse = mergedType === WAREHOUSE_LOCATION_TYPE && mergedActive === true;
    if (willBeActiveWarehouse && !wasActiveWarehouse) {
      await this.resourceLimits.assertWarehouseLimitForActivation(id);
    }

    const set: Record<string, unknown> = {};
    if (dto.name !== undefined) set.name = dto.name.trim();
    if (dto.address !== undefined) set.address = dto.address.trim();
    if (dto.type !== undefined) set.type = mergedType;
    if (dto.isActive !== undefined) set.isActive = dto.isActive;
    const doc = await this.model.findByIdAndUpdate(id, { $set: set }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Not found');
    return doc;
  }

  async remove(id: string) {
    const doc = await this.model.findByIdAndDelete(id).lean();
    if (!doc) throw new NotFoundException('Not found');
    return doc;
  }
}
