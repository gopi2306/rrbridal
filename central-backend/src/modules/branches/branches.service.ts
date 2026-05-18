import { ConflictException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model, SortOrder } from 'mongoose';
import { CreateBranchDto } from './dto/create-branch.dto';
import { FilterBranchDto } from './dto/filter-branch.dto';
import { UpdateBranchDto } from './dto/update-branch.dto';
import { Branch, BranchDocument } from './schemas/branch.schema';

@Injectable()
export class BranchesService {
  constructor(@InjectModel(Branch.name) private readonly model: Model<BranchDocument>) {}

  async create(dto: CreateBranchDto) {
    const code = dto.code.trim().toLowerCase();
    const existing = await this.model.findOne({ code }).lean();
    if (existing) throw new ConflictException(`Code '${code}' already exists`);
    return await this.model.create({
      code,
      name: dto.name.trim(),
      address: dto.address?.trim(),
      phone: dto.phone?.trim(),
      isActive: dto.isActive ?? true,
    });
  }

  async findAll() {
    return await this.model.find().sort({ name: 1 }).lean();
  }

  async filter(dto: FilterBranchDto) {
    const filter: FilterQuery<BranchDocument> = {};

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
        { address: { $regex: dto.search, $options: 'i' } },
        { phone: { $regex: dto.search, $options: 'i' } },
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

  async update(id: string, dto: UpdateBranchDto) {
    const set: Record<string, unknown> = {};
    if (dto.name !== undefined) set.name = dto.name.trim();
    if (dto.address !== undefined) set.address = dto.address.trim();
    if (dto.phone !== undefined) set.phone = dto.phone.trim();
    if (dto.isActive !== undefined) set.isActive = dto.isActive;
    const doc = await this.model.findByIdAndUpdate(id, { $set: set }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Not found');
    return doc;
  }

  async remove(id: string) {
    const doc = await this.model
      .findByIdAndUpdate(id, { $set: { isActive: false } }, { new: true })
      .lean();
    if (!doc) throw new NotFoundException('Not found');
    return doc;
  }
}
