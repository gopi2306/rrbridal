import { BadRequestException, ConflictException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model, SortOrder } from 'mongoose';
import { roundMoney } from '../../common/money.util';
import { CreatePromotionSchemeDto } from './dto/create-promotion-scheme.dto';
import { FilterPromotionSchemeDto } from './dto/filter-promotion-scheme.dto';
import { UpdatePromotionSchemeDto } from './dto/update-promotion-scheme.dto';
import { PromotionBenefitDto } from './dto/promotion-scheme-shared.dto';
import { PromotionScheme, PromotionBenefit, PromotionSchemeDocument, PromotionSlab, PromotionItemBenefitMode, PromotionFreeOn } from './schemas/promotion-scheme.schema';

const FILTER_SORT_FIELDS = new Set(['code', 'name', 'priority', 'type', 'isActive', 'createdAt', 'updatedAt']);

@Injectable()
export class PromotionSchemesService {
  constructor(
    @InjectModel(PromotionScheme.name) private readonly model: Model<PromotionSchemeDocument>,
  ) {}

  async create(dto: CreatePromotionSchemeDto) {
    const code = dto.code.trim().toLowerCase();
    const existing = await this.model.findOne({ code }).lean();
    if (existing) throw new ConflictException(`Code '${code}' already exists`);

    this.validateBenefitForType(dto.type, dto.benefit);
    if (dto.type === 'slab') this.validateSlabs(dto.benefit.slabs);
    if (dto.type === 'combo') this.validateComboBenefit(dto.benefit);

    const doc = await this.model.create({
      code,
      name: dto.name.trim(),
      description: dto.description?.trim(),
      kind: dto.kind ?? 'scheme',
      type: dto.type,
      priority: dto.priority ?? 100,
      isActive: dto.isActive ?? true,
      stacking: dto.stacking ?? 'best_benefit',
      storeIds: (dto.storeIds ?? []).map((s) => s.trim()).filter(Boolean),
      validFrom: dto.validFrom ? new Date(dto.validFrom) : undefined,
      validTo: dto.validTo ? new Date(dto.validTo) : undefined,
      timeWindows: dto.timeWindows ?? [],
      conditions: this.normalizeConditions(dto.conditions),
      benefit: this.normalizeBenefit(dto.benefit),
    });
    return doc.toObject();
  }

  async findAll() {
    return await this.model.find({ deletedAt: { $exists: false } }).sort({ priority: 1, name: 1 }).lean();
  }

  async findById(id: string) {
    const doc = await this.model.findById(id).lean();
    if (!doc || doc.deletedAt) throw new NotFoundException('Not found');
    return doc;
  }

  async findByCode(code: string) {
    const doc = await this.model.findOne({ code: code.trim().toLowerCase(), deletedAt: { $exists: false } }).lean();
    if (!doc) throw new NotFoundException(`Not found: '${code}'`);
    return doc;
  }

  async update(id: string, dto: UpdatePromotionSchemeDto) {
    const existing = await this.model.findById(id).lean();
    if (!existing || existing.deletedAt) throw new NotFoundException('Not found');

    const type = dto.type ?? existing.type;
    if (dto.benefit !== undefined) {
      this.validateBenefitForType(type, dto.benefit);
      if (type === 'slab') this.validateSlabs(dto.benefit.slabs);
      if (type === 'combo') this.validateComboBenefit(dto.benefit);
    }

    const set: Record<string, unknown> = {};
    if (dto.name !== undefined) set.name = dto.name.trim();
    if (dto.description !== undefined) set.description = dto.description?.trim();
    if (dto.kind !== undefined) set.kind = dto.kind;
    if (dto.type !== undefined) set.type = dto.type;
    if (dto.priority !== undefined) set.priority = dto.priority;
    if (dto.isActive !== undefined) set.isActive = dto.isActive;
    if (dto.stacking !== undefined) set.stacking = dto.stacking;
    if (dto.storeIds !== undefined) set.storeIds = dto.storeIds.map((s) => s.trim()).filter(Boolean);
    if (dto.validFrom !== undefined) set.validFrom = dto.validFrom ? new Date(dto.validFrom) : null;
    if (dto.validTo !== undefined) set.validTo = dto.validTo ? new Date(dto.validTo) : null;
    if (dto.timeWindows !== undefined) set.timeWindows = dto.timeWindows;
    if (dto.conditions !== undefined) set.conditions = this.normalizeConditions(dto.conditions);
    if (dto.benefit !== undefined) set.benefit = this.normalizeBenefit(dto.benefit);

    const doc = await this.model.findByIdAndUpdate(id, { $set: set }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Not found');
    return doc;
  }

  async deactivate(id: string) {
    return await this.update(id, { isActive: false });
  }

  async softDelete(id: string) {
    const doc = await this.model
      .findByIdAndUpdate(id, { $set: { isActive: false, deletedAt: new Date() } }, { new: true })
      .lean();
    if (!doc) throw new NotFoundException('Not found');
    return doc;
  }

  async filter(dto: FilterPromotionSchemeDto) {
    const page = dto.page ?? 1;
    const limit = dto.limit ?? 50;
    const skip = (page - 1) * limit;
    const sortBy = FILTER_SORT_FIELDS.has(dto.sortBy ?? '') ? dto.sortBy! : 'priority';
    const sortOrder: SortOrder = dto.sortOrder === 'desc' ? -1 : 1;

    const filter: FilterQuery<PromotionSchemeDocument> = { deletedAt: { $exists: false } };

    if (dto.isActive !== undefined) filter.isActive = dto.isActive;
    if (dto.type) filter.type = dto.type;
    if (dto.code) filter.code = dto.code.trim().toLowerCase();

    if (dto.storeId?.trim()) {
      const sid = dto.storeId.trim();
      filter.$or = [{ storeIds: { $size: 0 } }, { storeIds: sid }];
    }

    if (dto.search?.trim()) {
      const q = dto.search.trim();
      const rx = new RegExp(q.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'i');
      filter.$and = [
        ...(Array.isArray(filter.$and) ? filter.$and : []),
        { $or: [{ code: rx }, { name: rx }, { description: rx }] },
      ];
    }

    const [items, total] = await Promise.all([
      this.model.find(filter).sort({ [sortBy]: sortOrder }).skip(skip).limit(limit).lean(),
      this.model.countDocuments(filter),
    ]);

    return {
      items,
      page,
      limit,
      total,
      totalPages: Math.ceil(total / limit),
    };
  }

  /** Sync pull: schemes changed since cursor, scoped to store. Includes soft-deleted as delete deltas. */
  async listDeltasForStore(storeId: string, cursorFilter: Record<string, unknown>, limit: number) {
    const storeScope = {
      $or: [{ storeIds: { $size: 0 } }, { storeIds: storeId }],
    };
    return await this.model
      .find({ ...cursorFilter, ...storeScope })
      .sort({ _id: 1 })
      .limit(limit)
      .lean();
  }

  private normalizeConditions(conditions?: CreatePromotionSchemeDto['conditions']) {
    return {
      skus: (conditions?.skus ?? []).map((s) => s.trim()).filter(Boolean),
      categoryIds: conditions?.categoryIds ?? [],
      brandIds: conditions?.brandIds ?? [],
      offerGroupIds: conditions?.offerGroupIds ?? [],
      minLineQty: conditions?.minLineQty,
      minBillAmount: conditions?.minBillAmount != null ? roundMoney(conditions.minBillAmount) : undefined,
      customerTypes: (conditions?.customerTypes ?? []).map((s) => s.trim()).filter(Boolean),
      customerCodes: (conditions?.customerCodes ?? []).map((s) => s.trim()).filter(Boolean),
      requiredSkus: (conditions?.requiredSkus ?? []).map((r) => ({
        sku: r.sku.trim(),
        requiredQty: r.requiredQty,
      })),
    };
  }

  private normalizeBenefit(benefit: PromotionBenefitDto): PromotionBenefit {
    const out: PromotionBenefit = {
      slabs: [],
      comboSkus: [],
    };
    if (benefit.mode != null && ['buy_x_get_y', 'percent_off', 'flat_off'].includes(benefit.mode)) {
      out.mode = benefit.mode as PromotionItemBenefitMode;
    }
    if (benefit.buyQty != null) out.buyQty = benefit.buyQty;
    if (benefit.getQty != null) out.getQty = benefit.getQty;
    if (benefit.freeOn != null && ['cheapest', 'highest'].includes(benefit.freeOn)) {
      out.freeOn = benefit.freeOn as PromotionFreeOn;
    }
    if (benefit.discountPercent != null) out.discountPercent = benefit.discountPercent;
    if (benefit.flatAmount != null) out.flatAmount = roundMoney(benefit.flatAmount);
    if (benefit.minBillAmount != null) out.minBillAmount = roundMoney(benefit.minBillAmount);
    if (benefit.fixedPrice != null) out.fixedPrice = roundMoney(benefit.fixedPrice);
    if (Array.isArray(benefit.slabs)) {
      out.slabs = benefit.slabs.map((s) => {
        const slab: PromotionSlab = {
          fromAmount: roundMoney(s.fromAmount),
          discountPercent: s.discountPercent,
        };
        if (s.toAmount != null) slab.toAmount = roundMoney(s.toAmount);
        return slab;
      });
    }
    if (Array.isArray(benefit.comboSkus)) {
      out.comboSkus = benefit.comboSkus.map((s) => s.trim()).filter(Boolean);
    }
    return out;
  }

  private validateBenefitForType(type: string, benefit: PromotionBenefitDto) {
    if (!benefit || typeof benefit !== 'object') {
      throw new BadRequestException('benefit is required');
    }
    if (type === 'item') {
      const mode = benefit.mode;
      if (!['buy_x_get_y', 'percent_off', 'flat_off'].includes(String(mode))) {
        throw new BadRequestException('item benefit.mode must be buy_x_get_y | percent_off | flat_off');
      }
    }
    if (type === 'bill') {
      if (benefit.discountPercent == null && benefit.flatAmount == null) {
        throw new BadRequestException('bill benefit requires discountPercent or flatAmount');
      }
    }
    if (type === 'slab' && !Array.isArray(benefit.slabs)) {
      throw new BadRequestException('slab benefit requires slabs[]');
    }
    if (type === 'combo') {
      if (!Array.isArray(benefit.comboSkus) || benefit.fixedPrice == null) {
        throw new BadRequestException('combo benefit requires comboSkus[] and fixedPrice');
      }
    }
  }

  private validateSlabs(slabs?: PromotionSlab[]) {
    if (!slabs?.length) throw new BadRequestException('At least one slab is required');
    const sorted = [...slabs].sort((a, b) => a.fromAmount - b.fromAmount);
    for (let i = 0; i < sorted.length; i++) {
      const s = sorted[i]!;
      if (s.toAmount != null && s.toAmount <= s.fromAmount) {
        throw new BadRequestException(`Slab toAmount must exceed fromAmount at index ${i}`);
      }
      if (i > 0) {
        const prev = sorted[i - 1]!;
        const prevEnd = prev.toAmount ?? Number.POSITIVE_INFINITY;
        if (s.fromAmount < prevEnd) {
          throw new BadRequestException(`Slab ranges overlap at index ${i}`);
        }
      }
    }
  }

  private validateComboBenefit(benefit: PromotionBenefitDto) {
    const skus = benefit.comboSkus;
    if (!Array.isArray(skus) || skus.length < 2) {
      throw new BadRequestException('combo benefit requires at least 2 comboSkus');
    }
  }
}
