import { ConflictException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model, SortOrder } from 'mongoose';
import {
  applyObjectIdArrayRefAnyFilter,
  applyObjectIdArrayRefContainsFilter,
  applyObjectIdRefFilter,
  isValidObjectIdString,
  normalizeObjectIdArrayField,
  PRODUCT_REF_OBJECT_ID_ARRAY_FIELDS,
  PRODUCT_REF_OBJECT_ID_FIELDS,
  stripInvalidObjectIdArrayRefs,
  stripInvalidObjectIdRefs,
  toObjectId,
} from '../../common/object-id.util';
import { MONEY_DECIMAL_PLACES } from '../../common/money.util';
import { CreateProductDto } from './dto/create-product.dto';
import { FilterProductDto } from './dto/filter-product.dto';
import { UpdateProductDto } from './dto/update-product.dto';
import { AuditActor, serializeAuditDocument } from '../audit-logs/audit-change.util';
import { AuditLogsService, LogProductChangeInput } from '../audit-logs/audit-logs.service';
import {
  appendOrUpdateMediaItem,
  normalizeMediaItemsInput,
  ProductMediaItemValue,
  ProductMediaSource,
  resolveProductMediaItems,
} from './product-media-items';
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
    const lean = serializeAuditDocument(this.withNormalizedMediaItems(doc.toObject())) as Record<
      string,
      unknown
    >;
    const auditInput: LogProductChangeInput = {
      productId: String(doc._id),
      sku: doc.sku,
      action: 'created',
      after: lean,
    };
    if (actor) auditInput.actor = actor;
    await this.auditLogs.logProductChange(auditInput);
    return this.withNormalizedMediaItems(doc.toObject());
  }

  private apiPublicOrigin(): string | undefined {
    return process.env.API_PUBLIC_ORIGIN?.trim() || undefined;
  }

    private withNormalizedMediaItems<T extends object>(doc: T): T & {
      mediaItems: ProductMediaItemValue[];
      colourIds?: unknown;
    } {
      const origin = this.apiPublicOrigin();
      const migrated = this.migrateLegacyColourIds(doc as Record<string, unknown>);
      const source = migrated as ProductMediaSource;
      const resolved = resolveProductMediaItems(source);
      const mediaItems =
        normalizeMediaItemsInput(resolved, origin) ??
        resolved.map((m) => {
          const item: ProductMediaItemValue = { url: m.url };
          if (m.description) item.description = m.description;
          return item;
        });
      const { mediaUrls: _legacy, colourId: _legacyColour, ...rest } = migrated as T & {
        mediaUrls?: unknown;
        colourId?: unknown;
      };
      return {
        ...(rest as T),
        mediaItems,
      };
    }

  private migrateLegacyColourIds<T extends Record<string, unknown>>(doc: T): T {
    const current = doc.colourIds;
    if (Array.isArray(current) && current.length > 0) {
      const { colourId: _legacy, ...rest } = doc;
      return rest as T;
    }

    const legacy = doc.colourId;
    if (legacy === undefined || legacy === null || legacy === '') return doc;

    const { colourId: _legacy, ...rest } = doc;
    return {
      ...rest,
      colourIds: [legacy],
    } as unknown as T;
  }

  private normalizeProductWritePayload(dto: Record<string, unknown>): Record<string, unknown> {
    const doc = { ...dto };
    const origin = this.apiPublicOrigin();

    if ('mediaItems' in doc && doc.mediaItems !== undefined) {
      const items = normalizeMediaItemsInput(doc.mediaItems, origin) ?? [];
      doc.mediaItems = items.map((item) => {
        const row: Record<string, unknown> = { url: item.url };
        if (item.description) row.description = item.description;
        if (item.colourIds?.length) {
          row.colourIds = normalizeObjectIdArrayField(item.colourIds) ?? [];
        }
        return row;
      });
      delete doc.mediaUrls;
    } else if ('mediaUrls' in doc && doc.mediaUrls !== undefined) {
      const urls = Array.isArray(doc.mediaUrls) ? doc.mediaUrls : [];
      doc.mediaItems =
        normalizeMediaItemsInput(
          urls.map((u) => (typeof u === 'string' ? { url: u } : u)),
          origin,
        ) ?? [];
      delete doc.mediaUrls;
    }

    stripInvalidObjectIdRefs(doc, PRODUCT_REF_OBJECT_ID_FIELDS);
    stripInvalidObjectIdArrayRefs(doc, PRODUCT_REF_OBJECT_ID_ARRAY_FIELDS);
    for (const field of PRODUCT_REF_OBJECT_ID_FIELDS) {
      const v = doc[field];
      if (typeof v === 'string' && isValidObjectIdString(v)) {
        doc[field] = toObjectId(v);
      }
    }

    if ('colourIds' in doc && doc.colourIds !== undefined) {
      doc.colourIds = normalizeObjectIdArrayField(doc.colourIds) ?? [];
      delete doc.colourId;
    } else if ('colourId' in doc && doc.colourId !== undefined && doc.colourId !== null && doc.colourId !== '') {
      const legacy = doc.colourId;
      if (typeof legacy === 'string' && isValidObjectIdString(legacy)) {
        doc.colourIds = [toObjectId(legacy)];
      }
      delete doc.colourId;
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
    return this.withNormalizedMediaItems(doc as Record<string, unknown>);
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
    const wroteMedia = 'mediaItems' in payload;
    if (wroteMedia && !changedFields.includes('mediaItems')) {
      changedFields.push('mediaItems');
    }

    const updateOps: Record<string, unknown> = { $set: payload };
    if (wroteMedia) {
      updateOps.$unset = { mediaUrls: 1 };
    }
    if ('colourIds' in payload) {
      updateOps.$unset = { ...(updateOps.$unset as Record<string, unknown> | undefined), colourId: 1 };
    }

    const doc = await this.productModel
      .findByIdAndUpdate(toObjectId(id), updateOps, { new: true })
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
    return this.withNormalizedMediaItems(doc as Record<string, unknown>);
  }

  async appendMediaItem(
    id: string,
    url: string,
    description?: string,
    colourIds?: string[],
    actor?: AuditActor,
  ) {
    const origin = this.apiPublicOrigin();
    const before = await this.productModel.findById(toObjectId(id)).lean();
    if (!before) throw new NotFoundException('Product not found');

    const existing = resolveProductMediaItems(before as { mediaItems?: unknown; mediaUrls?: unknown });
    const next = appendOrUpdateMediaItem(existing, url, description, origin, colourIds);
    if (
      next.length === existing.length &&
      next.every(
        (m, i) =>
          m.url === existing[i]?.url &&
          m.description === existing[i]?.description &&
          JSON.stringify(m.colourIds ?? []) === JSON.stringify(existing[i]?.colourIds ?? []),
      )
    ) {
      return this.withNormalizedMediaItems(before as Record<string, unknown>);
    }

    const persistedItems = next.map((item) => {
      const row: Record<string, unknown> = { url: item.url };
      if (item.description) row.description = item.description;
      if (item.colourIds?.length) {
        row.colourIds = normalizeObjectIdArrayField(item.colourIds) ?? [];
      }
      return row;
    });

    const doc = await this.productModel
      .findByIdAndUpdate(
        toObjectId(id),
        {
          $set: { mediaItems: persistedItems },
          $unset: { mediaUrls: 1 },
        },
        { new: true },
      )
      .lean();
    if (!doc) throw new NotFoundException('Product not found');

    const auditInput: LogProductChangeInput = {
      productId: id,
      action: 'updated',
      before: serializeAuditDocument(before) as Record<string, unknown>,
      after: serializeAuditDocument(doc) as Record<string, unknown>,
      changedFields: ['mediaItems'],
    };
    if (typeof doc.sku === 'string') auditInput.sku = doc.sku;
    if (actor) auditInput.actor = actor;
    await this.auditLogs.logProductChange(auditInput);
    return this.withNormalizedMediaItems(doc as Record<string, unknown>);
  }

  /** @deprecated Use appendMediaItem */
  async appendMediaUrl(id: string, url: string, actor?: AuditActor) {
    return await this.appendMediaItem(id, url, undefined, undefined, actor);
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
    const data = await this.populateProductRefLookups(rawData);
    return data.map((row) => this.withNormalizedMediaItems(row));
  }

  private async populateProductRefLookups(rawData: unknown[]): Promise<Record<string, unknown>[]> {
    const data = rawData.map((row) => {
      const migrated = this.migrateLegacyColourIds({ ...(row as Record<string, unknown>) });
      stripInvalidObjectIdRefs(migrated, PRODUCT_REF_OBJECT_ID_FIELDS);
      stripInvalidObjectIdArrayRefs(migrated, PRODUCT_REF_OBJECT_ID_ARRAY_FIELDS);
      return migrated;
    });
    await this.productModel.populate(data, [
      ...PRODUCT_REF_OBJECT_ID_FIELDS.map((path) => ({ path })),
      ...PRODUCT_REF_OBJECT_ID_ARRAY_FIELDS.map((path) => ({ path })),
    ]);
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

    const colourFilterIds: string[] = [];
    if (dto.colourId?.trim()) colourFilterIds.push(dto.colourId.trim());
    if (dto.colourIds?.length) {
      colourFilterIds.push(...dto.colourIds.map((id) => id.trim()).filter((id) => id.length > 0));
    }
    if (colourFilterIds.length === 1) {
      applyObjectIdArrayRefContainsFilter(filter, 'colourIds', colourFilterIds[0]);
    } else if (colourFilterIds.length > 1) {
      applyObjectIdArrayRefAnyFilter(filter, 'colourIds', colourFilterIds);
    }

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
      'colourTypeId',
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

    const data = (await this.populateProductRefLookups(rawData)).map((row) =>
      this.withNormalizedMediaItems(row),
    );

    return {
      data,
      total,
      page,
      limit,
      totalPages: Math.ceil(total / limit),
    };
  }

  async listDeltas(cursorFilter: Record<string, unknown>, limit: number) {
    const rows = await this.productModel
      .find(cursorFilter)
      .sort({ updatedAt: 1, _id: 1 })
      .limit(limit)
      .lean();
    return rows.map((row) => this.withNormalizedMediaItems(row as Record<string, unknown>));
  }
}
