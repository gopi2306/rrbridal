import { ConflictException, Inject, Injectable, NotFoundException, forwardRef } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { ResourceLimitsService } from '../resource-limits/resource-limits.service';
import { CreateStoreDto } from './dto/create-store.dto';
import { UpdateStoreDto } from './dto/update-store.dto';
import { Store, StoreDocument } from './schemas/store.schema';

@Injectable()
export class StoresService {
  constructor(
    @InjectModel(Store.name) private readonly storeModel: Model<StoreDocument>,
    @Inject(forwardRef(() => ResourceLimitsService)) private readonly resourceLimits: ResourceLimitsService,
  ) {}

  async create(dto: CreateStoreDto) {
    await this.resourceLimits.assertStoreLimit();
    const code = dto.code.trim().toLowerCase();
    const existing = await this.storeModel.findOne({ code }).lean();
    if (existing) throw new ConflictException(`Store code '${code}' already exists`);
    return await this.storeModel.create({
      code,
      name: dto.name.trim(),
      address: dto.address?.trim(),
      phone: dto.phone?.trim(),
      status: 'active',
    });
  }

  async findAll() {
    return await this.storeModel.find().sort({ code: 1 }).lean();
  }

  async findByCode(code: string) {
    const doc = await this.storeModel.findOne({ code: code.trim().toLowerCase() }).lean();
    if (!doc) throw new NotFoundException(`Store '${code}' not found`);
    return doc;
  }

  async update(code: string, dto: UpdateStoreDto) {
    const set: Record<string, unknown> = {};
    if (dto.name !== undefined) set.name = dto.name.trim();
    if (dto.address !== undefined) set.address = dto.address.trim();
    if (dto.phone !== undefined) set.phone = dto.phone.trim();
    if (dto.status !== undefined) set.status = dto.status;
    if (Object.keys(set).length === 0) {
      return await this.findByCode(code);
    }
    const doc = await this.storeModel
      .findOneAndUpdate({ code: code.trim().toLowerCase() }, { $set: set }, { new: true })
      .lean();
    if (!doc) throw new NotFoundException(`Store '${code}' not found`);
    return doc;
  }

  async existsByCode(code: string): Promise<boolean> {
    const count = await this.storeModel.countDocuments({ code: code.trim().toLowerCase() });
    return count > 0;
  }
}
