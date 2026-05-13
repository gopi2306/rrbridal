import { ConflictException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { CreateHsnCodeDto } from './dto/create-hsn-code.dto';
import { UpdateHsnCodeDto } from './dto/update-hsn-code.dto';
import { HsnCode, HsnCodeDocument } from './schemas/hsn-code.schema';

@Injectable()
export class HsnCodesService {
  constructor(@InjectModel(HsnCode.name) private readonly model: Model<HsnCodeDocument>) {}

  async create(dto: CreateHsnCodeDto) {
    const code = dto.code.trim().toLowerCase();
    const existing = await this.model.findOne({ code }).lean();
    if (existing) throw new ConflictException(`Code '${code}' already exists`);
    return await this.model.create({
      code,
      name: dto.name.trim(),
      hsnCode: dto.hsnCode.trim(),
      description: dto.description,
      gstPercent: dto.gstPercent,
      isActive: dto.isActive ?? true,
    });
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

  async update(id: string, dto: UpdateHsnCodeDto) {
    const set: Record<string, unknown> = {};
    if (dto.name !== undefined) set.name = dto.name.trim();
    if (dto.hsnCode !== undefined) set.hsnCode = dto.hsnCode.trim();
    if (dto.description !== undefined) set.description = dto.description;
    if (dto.gstPercent !== undefined) set.gstPercent = dto.gstPercent;
    if (dto.isActive !== undefined) set.isActive = dto.isActive;
    const doc = await this.model.findByIdAndUpdate(id, { $set: set }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Not found');
    return doc;
  }
}
