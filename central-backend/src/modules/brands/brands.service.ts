import { ConflictException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model, SortOrder } from 'mongoose';
import { CreateBrandDto } from './dto/create-brand.dto';
import { FilterBrandDto } from './dto/filter-brand.dto';
import { UpdateBrandDto } from './dto/update-brand.dto';
import { Brand, BrandDocument } from './schemas/brand.schema';

@Injectable()
export class BrandsService {
  constructor(@InjectModel(Brand.name) private readonly model: Model<BrandDocument>) {}

  async create(dto: CreateBrandDto) {
    const code = dto.code.trim().toLowerCase();
    const existing = await this.model.findOne({ code }).lean();
    if (existing) throw new ConflictException(`Code '${code}' already exists`);
    return await this.model.create({ code, name: dto.name.trim(), isActive: dto.isActive ?? true });
  }

  async findAll() {
    return await this.model.find().sort({ name: 1 }).lean();
  }

  async filter(dto: FilterBrandDto) {
    const filter: FilterQuery<BrandDocument> = {};

    if (dto.code !== undefined && dto.code !== '') {
      filter.code = dto.code.trim().toLowerCase();
    }

    if (dto.isActive !== undefined && dto.isActive !== null) {
      filter.isActive = dto.isActive;
    }

    if (dto.search) {
      filter.$or = [
        { code: { $regex: dto.search, $options: 'i' } },
        { name: { $regex: dto.search, $options: 'i' } },
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

  async findByCode(code: string) {
    const doc = await this.model.findOne({ code: code.trim().toLowerCase() }).lean();
    if (!doc) throw new NotFoundException(`Not found: '${code}'`);
    return doc;
  }

  async findById(id: string) {
    const doc = await this.model.findById(id).lean();
    if (!doc) throw new NotFoundException('Not found');
    return doc;
  }

  async update(id: string, dto: UpdateBrandDto) {
    const set: Record<string, unknown> = {};
    if (dto.name !== undefined) set.name = dto.name.trim();
    if (dto.isActive !== undefined) set.isActive = dto.isActive;
    const doc = await this.model.findByIdAndUpdate(id, { $set: set }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Not found');
    return doc;
  }
}
