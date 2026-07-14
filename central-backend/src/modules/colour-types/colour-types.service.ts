import { ConflictException, Injectable, NotFoundException, OnModuleInit } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { CreateColourTypeDto } from './dto/create-colour-type.dto';
import { UpdateColourTypeDto } from './dto/update-colour-type.dto';
import { ColourType, ColourTypeDocument } from './schemas/colour-type.schema';

const DEFAULT_COLOUR_TYPES: ReadonlyArray<{ code: string; name: string }> = [
  { code: 'ct-1', name: '1 Color' },
  { code: 'ct-2', name: '2 Color' },
  { code: 'ct-3', name: '3 Color' },
];

@Injectable()
export class ColourTypesService implements OnModuleInit {
  constructor(@InjectModel(ColourType.name) private readonly model: Model<ColourTypeDocument>) {}

  async onModuleInit() {
    await this.ensureDefaults();
  }

  async ensureDefaults() {
    for (const row of DEFAULT_COLOUR_TYPES) {
      await this.model.updateOne(
        { code: row.code },
        {
          $setOnInsert: {
            code: row.code,
            name: row.name,
            isActive: true,
          },
        },
        { upsert: true },
      );
    }
  }

  async create(dto: CreateColourTypeDto) {
    const code = dto.code.trim().toLowerCase();
    const existing = await this.model.findOne({ code }).lean();
    if (existing) throw new ConflictException(`Code '${code}' already exists`);
    return await this.model.create({
      code,
      name: dto.name.trim(),
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

  async update(id: string, dto: UpdateColourTypeDto) {
    const set: Record<string, unknown> = {};
    if (dto.name !== undefined) set.name = dto.name.trim();
    if (dto.isActive !== undefined) set.isActive = dto.isActive;
    const doc = await this.model.findByIdAndUpdate(id, { $set: set }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Not found');
    return doc;
  }
}
