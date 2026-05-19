import { ConflictException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { CreateSubCategoryDto } from './dto/create-sub-category.dto';
import { UpdateSubCategoryDto } from './dto/update-sub-category.dto';
import { SubCategory, SubCategoryDocument } from './schemas/sub-category.schema';

const SUB_CATEGORY_CODE_PREFIX = 'subcat-';

@Injectable()
export class SubCategoriesService {
  constructor(@InjectModel(SubCategory.name) private readonly model: Model<SubCategoryDocument>) {}

  /** Next sequential code: subcat-001, subcat-002, … */
  private async nextSubCategoryCode(): Promise<string> {
    const pattern = new RegExp(`^${SUB_CATEGORY_CODE_PREFIX}\\d+$`, 'i');
    const rows = await this.model.find({ code: pattern }).select('code').lean();
    let max = 0;
    for (const row of rows) {
      const suffix = row.code.slice(SUB_CATEGORY_CODE_PREFIX.length);
      const n = parseInt(suffix, 10);
      if (!Number.isNaN(n) && n > max) max = n;
    }
    return `${SUB_CATEGORY_CODE_PREFIX}${String(max + 1).padStart(3, '0')}`;
  }

  async create(dto: CreateSubCategoryDto) {
    for (let attempt = 0; attempt < 5; attempt++) {
      const code = await this.nextSubCategoryCode();
      const existing = await this.model.findOne({ code }).lean();
      if (existing) continue;
      try {
        return await this.model.create({
          code,
          name: dto.name.trim(),
          categoryId: dto.categoryId,
          isActive: dto.isActive ?? true,
        });
      } catch (err: unknown) {
        const mongo = err as { code?: number };
        if (mongo.code === 11000 && attempt < 4) continue;
        throw err;
      }
    }
    throw new ConflictException('Could not allocate a unique sub-category code');
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

  async update(id: string, dto: UpdateSubCategoryDto) {
    const set: Record<string, unknown> = {};
    if (dto.name !== undefined) set.name = dto.name.trim();
    if (dto.categoryId !== undefined) set.categoryId = dto.categoryId;
    if (dto.isActive !== undefined) set.isActive = dto.isActive;
    const doc = await this.model.findByIdAndUpdate(id, { $set: set }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Not found');
    return doc;
  }
}
