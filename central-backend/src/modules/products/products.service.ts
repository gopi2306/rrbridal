import { BadRequestException, ConflictException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model, SortOrder } from 'mongoose';
import {
  applyObjectIdRefFilter,
  isValidObjectIdString,
  PRODUCT_REF_OBJECT_ID_FIELDS,
  stripInvalidObjectIdRefs,
  toObjectId,
} from '../../common/object-id.util';
import { MONEY_DECIMAL_PLACES } from '../../common/money.util';
import { CreateProductDto } from './dto/create-product.dto';
import { FilterProductDto } from './dto/filter-product.dto';
import { UpdateProductDto } from './dto/update-product.dto';
import { AuditActor, serializeAuditDocument } from '../audit-logs/audit-change.util';
import { AuditLogsService, LogProductChangeInput } from '../audit-logs/audit-logs.service';
import { ProductSkuGenerator } from './product-sku.generator';
import { Product, ProductDocument } from './schemas/product.schema';

export type ProductListFilterParams = {
  search?: string;
  sku?: string;
  skuContains?: string;
  /** When set, only products whose sku is in this list (used by inventory grid store filter). */
  skus?: string[];
  upcEanCode?: string;
  categoryId?: string;
  supplierNameId?: string;
};

function escapeRegex(s: string): string {
  return s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

@Injectable()
export class ProductsService {
  constructor(
    @InjectModel(Product.name) private readonly productModel: Model<ProductDocument>,
    private readonly skuGenerator: ProductSkuGenerator,
    private readonly auditLogs: AuditLogsService,
  ) {}

  async create(dto: CreateProductDto, actor?: AuditActor) {
    const sku = await this.resolveSkuForCreate(dto.sku);
    const payload = this.normalizeProductWritePayload({
      ...dto,
      sku,
      isActive: dto.isActive ?? true,
      decimalPoint: dto.decimalPoint ?? MONEY_DECIMAL_PLACES,
    });
    const doc = await this.productModel.create(payload);
    const lean = serializeAuditDocument(doc.toObject()) as Record<string, unknown>;
    const auditInput: LogProductChangeInput = {
      productId: String(doc._id),
      sku: doc.sku,
      action: 'created',
      after: lean,
    };
    if (actor) auditInput.actor = actor;
    await this.auditLogs.logProductChange(auditInput);
    return doc;
  }

  private normalizeProductWritePayload(dto: Record<string, unknown>): Record<string, unknown> {
    const doc = { ...dto };
    stripInvalidObjectIdRefs(doc, PRODUCT_REF_OBJECT_ID_FIELDS);
    for (const field of PRODUCT_REF_OBJECT_ID_FIELDS) {
      const v = doc[field];
      if (typeof v === 'string' && isValidObjectIdString(v)) {
        doc[field] = toObjectId(v);
      }
    }
    return doc;
  }

  private async resolveSkuForCreate(skuInput?: string): Promise<string> {
    const trimmed = skuInput?.trim();
    if (trimmed) {
      const exists = await this.productModel.exists({ sku: trimmed }).lean();
      if (exists) throw new ConflictException(`SKU '${trimmed}' is already in use`);
      return trimmed;
    }
    return await this.skuGenerator.allocateNextAsync();
  }

  async findBySku(sku: string) {
    const trimmed = sku?.trim();
    if (!trimmed) return null;
    return await this.productModel.findOne({ sku: trimmed }).lean();
  }

  /** Import upsert lookup: match by SKU first, then exact itemName (description). */
  async findExistingForImport(sku?: string, itemName?: string) {
    const skuTrim = sku?.trim();
    if (skuTrim) {
      const bySku = await this.findBySku(skuTrim);
      if (bySku) return bySku;
    }

    const nameTrim = itemName?.trim();
    if (nameTrim) {
      return await this.productModel.findOne({ itemName: nameTrim }).lean();
    }

    return null;
  }

  async findById(id: string) {
    if (!isValidObjectIdString(id)) throw new NotFoundException('Product not found');
    const doc = await this.productModel.findById(toObjectId(id)).lean();
    if (!doc) throw new NotFoundException('Product not found');
    return doc;
  }

  /** Create or update by SKU, or by itemName when SKU is missing or not found. */
  async upsertBySku(
    dto: CreateProductDto,
    actor?: AuditActor,
  ): Promise<{ created: boolean; product: unknown }> {
    const existing = await this.findExistingForImport(dto.sku, dto.itemName);
    if (existing) {
      const { sku: skuInput, ...rest } = dto;
      const payload = { ...rest } as UpdateProductDto;
      const newSku = skuInput?.trim();
      if (newSku) {
        const conflict = await this.productModel
          .findOne({ sku: newSku, _id: { $ne: existing._id } })
          .lean();
        if (conflict) {
          throw new ConflictException(`SKU '${newSku}' is already in use`);
        }
        payload.sku = newSku;
      }
      const product = await this.update(String(existing._id), payload, actor);
      return { created: false, product };
    }

    const product = await this.create(dto, actor);
    return { created: true, product };
  }

  async update(id: string, dto: UpdateProductDto, actor?: AuditActor) {
    if (!isValidObjectIdString(id)) throw new NotFoundException('Product not found');
    const before = await this.productModel.findById(toObjectId(id)).lean();
    if (!before) throw new NotFoundException('Product not found');

    const payload = this.normalizeProductWritePayload({ ...dto } as Record<string, unknown>);
    const changedFields = Object.keys(dto).filter((k) => (dto as Record<string, unknown>)[k] !== undefined);
    const doc = await this.productModel
      .findByIdAndUpdate(toObjectId(id), { $set: payload }, { new: true })
      .lean();
    if (!doc) throw new NotFoundException('Product not found');

    const auditInput: LogProductChangeInput = {
      productId: id,
      action: 'updated',
      before: serializeAuditDocument(before) as Record<string, unknown>,
      after: serializeAuditDocument(doc) as Record<string, unknown>,
      changedFields,
    };
    if (typeof doc.sku === 'string') auditInput.sku = doc.sku;
    if (actor) auditInput.actor = actor;
    await this.auditLogs.logProductChange(auditInput);
    return doc;
  }

  async list(params: ProductListFilterParams & { skip?: number; limit?: number }) {
    const filter = this.buildProductListFilter(params);
    const skip = params.skip !== undefined ? Math.max(0, params.skip) : 0;
    const limit = params.limit !== undefined ? Math.min(500, Math.max(1, params.limit)) : 200;
    const rawData = await this.productModel
      .find(filter)
      .sort({ updatedAt: -1 })
      .skip(skip)
      .limit(limit)
      .lean();
    return await this.populateProductRefLookups(rawData);
  }

  /** Expand each ObjectId ref to its master document (same as POST /products/filter). */
  private async populateProductRefLookups(rawData: unknown[]): Promise<Record<string, unknown>[]> {
    const data = rawData.map((row) =>
      stripInvalidObjectIdRefs({ ...(row as Record<string, unknown>) }, PRODUCT_REF_OBJECT_ID_FIELDS),
    );
    await this.productModel.populate(
      data,
      PRODUCT_REF_OBJECT_ID_FIELDS.map((path) => ({ path })),
    );
    return data;
  }

  /** Same filter rules as {@link list}, without pagination — for counts. */
  async countForListFilter(params: ProductListFilterParams) {
    return await this.productModel.countDocuments(this.buildProductListFilter(params));
  }

  private buildProductListFilter(params: ProductListFilterParams): FilterQuery<ProductDocument> {
    const filter: FilterQuery<ProductDocument> = {};

    const skuExact = params.sku?.trim();
    const skuPartial = params.skuContains?.trim();
    if (params.skus?.length) {
      filter.sku = { $in: params.skus };
    } else if (skuExact) {
      filter.sku = skuExact;
    } else if (skuPartial) {
      filter.sku = { $regex: escapeRegex(skuPartial), $options: 'i' };
    }

    const upc = params.upcEanCode?.trim();
    if (upc) filter.upcEanCode = upc;

    applyObjectIdRefFilter(filter, 'categoryId', params.categoryId);
    applyObjectIdRefFilter(filter, 'supplierNameId', params.supplierNameId);

    const search = params.search?.trim();
    if (search) {
      const rx = { $regex: escapeRegex(search), $options: 'i' };
      filter.$or = [
        { itemName: rx },
        { shortName: rx },
        { alias: rx },
        { sku: rx },
        { upcEanCode: rx },
      ];
    }

    return filter;
  }

  async filter(dto: FilterProductDto) {
    const filter: FilterQuery<ProductDocument> = {};

    applyObjectIdRefFilter(
      filter,
      'supplierNameId',
      dto.supplierNameId ?? dto.supplierId,
    );

    const exactMatchFields = [
      'upcEanCode',
      'manufacturerNameId',
      // supplierNameId handled above (supports supplierId alias)
      'departmentId',
      'categoryId',
      'subCategoryId',
      'brandId',
      'weightAndSizeId',
      'weightPerGmOrMlId',
      'offerGroupId',
      'productStatusId',
      'colourId',
      'hsnCodeId',
      'gstUomId',
      'uomSubId',
      'batchExpiryDetailId',
      'itemPrepStatusId',
      'packedConfirmationId',
      'poQtyPolicyId',
      'sellById',
      'batchSelectionId',
      'skuTypeId',
      'skuOrderGroupId',
      'indentTypeId',
    ] as const;

    for (const field of exactMatchFields) {
      applyObjectIdRefFilter(filter, field, dto[field]);
    }

    if (dto.isActive !== undefined && dto.isActive !== null) {
      filter.isActive = dto.isActive;
    }

    if (dto.mrpMin !== undefined || dto.mrpMax !== undefined) {
      filter.mrp = {};
      if (dto.mrpMin !== undefined) filter.mrp.$gte = dto.mrpMin;
      if (dto.mrpMax !== undefined) filter.mrp.$lte = dto.mrpMax;
    }

    if (dto.sellingPriceMin !== undefined || dto.sellingPriceMax !== undefined) {
      filter.sellingPrice = {};
      if (dto.sellingPriceMin !== undefined) filter.sellingPrice.$gte = dto.sellingPriceMin;
      if (dto.sellingPriceMax !== undefined) filter.sellingPrice.$lte = dto.sellingPriceMax;
    }

    const skuExact = dto.sku?.trim();
    const skuPartial = dto.skuContains?.trim();
    if (skuExact) {
      filter.sku = skuExact;
    } else if (skuPartial) {
      filter.sku = { $regex: escapeRegex(skuPartial), $options: 'i' };
    }

    const searchText = dto.search?.trim();
    if (searchText) {
      const rx = { $regex: escapeRegex(searchText), $options: 'i' };
      filter.$or = [
        { itemName: rx },
        { shortName: rx },
        { alias: rx },
        { sku: rx },
        { upcEanCode: rx },
      ];
    }

    const page = dto.page ?? 1;
    const limit = dto.limit ?? 20;
    const skip = (page - 1) * limit;
    const sortBy = dto.sortBy ?? 'updatedAt';
    const sortOrder: SortOrder = dto.sortOrder === 'asc' ? 1 : -1;

    const [rawData, total] = await Promise.all([
      this.productModel
        .find(filter)
        .sort({ [sortBy]: sortOrder })
        .skip(skip)
        .limit(limit)
        .lean(),
      this.productModel.countDocuments(filter),
    ]);

    const data = await this.populateProductRefLookups(rawData);

    return {
      data,
      total,
      page,
      limit,
      totalPages: Math.ceil(total / limit),
    };
  }

  async listDeltas(cursorFilter: Record<string, unknown>, limit: number) {
    return await this.productModel.find(cursorFilter).sort({ _id: 1 }).limit(limit).lean();
  }
}

