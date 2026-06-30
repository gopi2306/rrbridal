import { ConflictException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model } from 'mongoose';
import { CreateSalesmanDto } from './dto/create-salesman.dto';
import { UpdateSalesmanDto } from './dto/update-salesman.dto';
import { SalesmanCodeGenerator } from './salesman-code.generator';
import { Salesman, SalesmanDocument } from './schemas/salesman.schema';

@Injectable()
export class SalesmenService {
  constructor(
    @InjectModel(Salesman.name) private readonly salesmanModel: Model<SalesmanDocument>,
    private readonly salesmanCodeGenerator: SalesmanCodeGenerator,
  ) {}

  async create(dto: CreateSalesmanDto) {
    const normalized = this.normalizeCreateDto(dto);
    let salesmanCode = normalized.salesmanCode;

    if (salesmanCode) {
      const existing = await this.findByCode(normalized.storeId, salesmanCode);
      if (existing) {
        throw new ConflictException(`Salesman code "${salesmanCode}" already exists for this store`);
      }
    } else {
      salesmanCode = await this.salesmanCodeGenerator.allocateNextAsync(normalized.storeId);
    }

    try {
      return await this.salesmanModel.create({
        ...normalized,
        salesmanCode,
        isActive: normalized.isActive ?? true,
      });
    } catch (err: unknown) {
      if (!this.isDuplicateKey(err)) throw err;
      const freshCode = await this.salesmanCodeGenerator.allocateNextAsync(normalized.storeId);
      return await this.salesmanModel.create({
        ...normalized,
        salesmanCode: freshCode,
        isActive: normalized.isActive ?? true,
      });
    }
  }

  async findById(id: string) {
    const doc = await this.salesmanModel.findById(id).lean();
    if (!doc) throw new NotFoundException('Salesman not found');
    return doc;
  }

  async findByCode(storeId: string, salesmanCode: string) {
    const trimmedStoreId = storeId?.trim();
    const trimmedCode = salesmanCode?.trim();
    if (!trimmedStoreId || !trimmedCode) return null;
    return await this.salesmanModel.findOne({ storeId: trimmedStoreId, salesmanCode: trimmedCode }).lean();
  }

  async listByStore(storeId: string, search?: string) {
    const trimmedStoreId = storeId?.trim();
    if (!trimmedStoreId) return [];

    const filter: FilterQuery<SalesmanDocument> = { storeId: trimmedStoreId };
    if (search?.trim()) {
      const term = search.trim();
      filter.$or = [
        { name: { $regex: term, $options: 'i' } },
        { phone: { $regex: term, $options: 'i' } },
        { salesmanCode: { $regex: term, $options: 'i' } },
      ];
    }

    return await this.salesmanModel.find(filter).sort({ isActive: -1, name: 1 }).limit(500).lean();
  }

  async update(id: string, dto: UpdateSalesmanDto) {
    const normalized = this.normalizeUpdateDto(dto);
    const doc = await this.salesmanModel.findByIdAndUpdate(id, { $set: normalized }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Salesman not found');
    return doc;
  }

  private normalizeCreateDto(dto: CreateSalesmanDto): CreateSalesmanDto {
    const trim = (v?: string) => {
      const t = v?.trim();
      return t ? t : undefined;
    };

    const normalized: CreateSalesmanDto = {
      storeId: dto.storeId.trim(),
      name: dto.name.trim(),
    };

    const salesmanCode = trim(dto.salesmanCode);
    if (salesmanCode) normalized.salesmanCode = salesmanCode;

    const phone = trim(dto.phone);
    if (phone) normalized.phone = phone;

    if (dto.isActive !== undefined) normalized.isActive = dto.isActive;

    return normalized;
  }

  private normalizeUpdateDto(dto: UpdateSalesmanDto): UpdateSalesmanDto {
    const normalized: UpdateSalesmanDto = {};
    if (dto.name !== undefined) normalized.name = dto.name.trim();
    if (dto.phone !== undefined) {
      const phone = dto.phone.trim();
      if (phone.length > 0) normalized.phone = phone;
    }
    if (dto.isActive !== undefined) normalized.isActive = dto.isActive;
    return normalized;
  }

  private isDuplicateKey(err: unknown): boolean {
    return !!(
      err &&
      typeof err === 'object' &&
      'code' in err &&
      (err as { code?: number }).code === 11000
    );
  }
}
