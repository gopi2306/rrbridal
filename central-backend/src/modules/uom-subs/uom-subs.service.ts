import { ConflictException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { CreateUomSubDto } from './dto/create-uom-sub.dto';
import { UpdateUomSubDto } from './dto/update-uom-sub.dto';
import { UomSub, UomSubDocument } from './schemas/uom-sub.schema';

@Injectable()
export class UomSubsService {
  constructor(@InjectModel(UomSub.name) private readonly model: Model<UomSubDocument>) {}

  async create(dto: CreateUomSubDto) {
    const code = dto.code.trim().toLowerCase();
    const existing = await this.model.findOne({ code }).lean();
    if (existing) throw new ConflictException(`Code '${code}' already exists`);
    return await this.model.create({ code, name: dto.name.trim(), baseUom: dto.baseUom, conversionFactor: dto.conversionFactor, isActive: dto.isActive ?? true });
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

  async update(id: string, dto: UpdateUomSubDto) {
    const set: Record<string, unknown> = {};
    if (dto.name !== undefined) set.name = dto.name.trim();
    if (dto.baseUom !== undefined) set.baseUom = dto.baseUom;
    if (dto.conversionFactor !== undefined) set.conversionFactor = dto.conversionFactor;
    if (dto.isActive !== undefined) set.isActive = dto.isActive;
    const doc = await this.model.findByIdAndUpdate(id, { $set: set }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Not found');
    return doc;
  }
}
