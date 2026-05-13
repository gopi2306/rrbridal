import { ConflictException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { CreatePoQtyPolicyDto } from './dto/create-po-qty-policy.dto';
import { UpdatePoQtyPolicyDto } from './dto/update-po-qty-policy.dto';
import { PoQtyPolicy, PoQtyPolicyDocument } from './schemas/po-qty-policy.schema';

@Injectable()
export class PoQtyPoliciesService {
  constructor(@InjectModel(PoQtyPolicy.name) private readonly model: Model<PoQtyPolicyDocument>) {}

  async create(dto: CreatePoQtyPolicyDto) {
    const code = dto.code.trim().toLowerCase();
    const existing = await this.model.findOne({ code }).lean();
    if (existing) throw new ConflictException(`Code '${code}' already exists`);
    return await this.model.create({ code, name: dto.name.trim(), description: dto.description, minQty: dto.minQty, maxQty: dto.maxQty, isActive: dto.isActive ?? true });
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

  async update(id: string, dto: UpdatePoQtyPolicyDto) {
    const set: Record<string, unknown> = {};
    if (dto.name !== undefined) set.name = dto.name.trim();
    if (dto.description !== undefined) set.description = dto.description;
    if (dto.minQty !== undefined) set.minQty = dto.minQty;
    if (dto.maxQty !== undefined) set.maxQty = dto.maxQty;
    if (dto.isActive !== undefined) set.isActive = dto.isActive;
    const doc = await this.model.findByIdAndUpdate(id, { $set: set }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Not found');
    return doc;
  }
}
