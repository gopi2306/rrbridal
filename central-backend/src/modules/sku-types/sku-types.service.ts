import { ConflictException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { CreateSkuTypeDto } from './dto/create-sku-type.dto';
import { UpdateSkuTypeDto } from './dto/update-sku-type.dto';
import { SkuType, SkuTypeDocument } from './schemas/sku-type.schema';

@Injectable()
export class SkuTypesService {
  constructor(@InjectModel(SkuType.name) private readonly model: Model<SkuTypeDocument>) {}

  async create(dto: CreateSkuTypeDto) {
    const code = dto.code.trim().toLowerCase();
    const existing = await this.model.findOne({ code }).lean();
    if (existing) throw new ConflictException(`Code '${code}' already exists`);
    return await this.model.create({ code, name: dto.name.trim(), isActive: dto.isActive ?? true });
  }

  async findAll() {
    return await this.model.find().sort({ name: 1 }).lean();
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

  async update(id: string, dto: UpdateSkuTypeDto) {
    const set: Record<string, unknown> = {};
    if (dto.name !== undefined) set.name = dto.name.trim();
    if (dto.isActive !== undefined) set.isActive = dto.isActive;
    const doc = await this.model.findByIdAndUpdate(id, { $set: set }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Not found');
    return doc;
  }
}
