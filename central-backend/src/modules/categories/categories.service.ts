import { ConflictException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { CreateCategoryDto } from './dto/create-category.dto';
import { UpdateCategoryDto } from './dto/update-category.dto';
import { Category, CategoryDocument } from './schemas/category.schema';

const CATEGORY_CODE_PREFIX = 'cat-';

@Injectable()
export class CategoriesService {
  constructor(@InjectModel(Category.name) private readonly model: Model<CategoryDocument>) {}

  /** Next sequential code: cat-001, cat-002, … */
  private async nextCategoryCode(): Promise<string> {
    const pattern = new RegExp(`^${CATEGORY_CODE_PREFIX}\\d+$`, 'i');
    const rows = await this.model.find({ code: pattern }).select('code').lean();
    let max = 0;
    for (const row of rows) {
      const suffix = row.code.slice(CATEGORY_CODE_PREFIX.length);
      const n = parseInt(suffix, 10);
      if (!Number.isNaN(n) && n > max) max = n;
    }
    return `${CATEGORY_CODE_PREFIX}${String(max + 1).padStart(3, '0')}`;
  }

  async create(dto: CreateCategoryDto) {
    for (let attempt = 0; attempt < 5; attempt++) {
      const code = await this.nextCategoryCode();
      const existing = await this.model.findOne({ code }).lean();
      if (existing) continue;
      try {
        return await this.model.create({
          code,
          name: dto.name.trim(),
          departmentId: dto.departmentId,
          isActive: dto.isActive ?? true,
        });
      } catch (err: unknown) {
        const mongo = err as { code?: number };
        if (mongo.code === 11000 && attempt < 4) continue;
        throw err;
      }
    }
    throw new ConflictException('Could not allocate a unique category code');
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

  async update(id: string, dto: UpdateCategoryDto) {
    const set: Record<string, unknown> = {};
    if (dto.name !== undefined) set.name = dto.name.trim();
    if (dto.departmentId !== undefined) set.departmentId = dto.departmentId;
    if (dto.isActive !== undefined) set.isActive = dto.isActive;
    const doc = await this.model.findByIdAndUpdate(id, { $set: set }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Not found');
    return doc;
  }
}
