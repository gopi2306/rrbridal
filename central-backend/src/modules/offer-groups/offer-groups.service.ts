import { ConflictException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { CreateOfferGroupDto } from './dto/create-offer-group.dto';
import { UpdateOfferGroupDto } from './dto/update-offer-group.dto';
import { OfferGroup, OfferGroupDocument } from './schemas/offer-group.schema';

@Injectable()
export class OfferGroupsService {
  constructor(@InjectModel(OfferGroup.name) private readonly model: Model<OfferGroupDocument>) {}

  async create(dto: CreateOfferGroupDto) {
    const code = dto.code.trim().toLowerCase();
    const existing = await this.model.findOne({ code }).lean();
    if (existing) throw new ConflictException(`Code '${code}' already exists`);
    return await this.model.create({ code, name: dto.name.trim(), description: dto.description, discountPercent: dto.discountPercent, validFrom: dto.validFrom, validTo: dto.validTo, isActive: dto.isActive ?? true });
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

  async update(id: string, dto: UpdateOfferGroupDto) {
    const set: Record<string, unknown> = {};
    if (dto.name !== undefined) set.name = dto.name.trim();
    if (dto.description !== undefined) set.description = dto.description;
    if (dto.discountPercent !== undefined) set.discountPercent = dto.discountPercent;
    if (dto.validFrom !== undefined) set.validFrom = dto.validFrom;
    if (dto.validTo !== undefined) set.validTo = dto.validTo;
    if (dto.isActive !== undefined) set.isActive = dto.isActive;
    const doc = await this.model.findByIdAndUpdate(id, { $set: set }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Not found');
    return doc;
  }
}
