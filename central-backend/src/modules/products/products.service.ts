import { BadRequestException, ConflictException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model, SortOrder } from 'mongoose';
import {
  isValidObjectIdString,
  objectIdRefEquals,
  PRODUCT_REF_OBJECT_ID_FIELDS,
  stripInvalidObjectIdRefs,
  toObjectId,
} from '../../common/object-id.util';
import { MONEY_DECIMAL_PLACES } from '../../common/money.util';
import { CreateProductDto } from './dto/create-product.dto';
import { FilterProductDto } from './dto/filter-product.dto';
import { UpdateProductDto } from './dto/update-product.dto';
import { ProductSkuGenerator } from './product-sku.generator';
import { Product, ProductDocument } from './schemas/product.schema';

export type ProductListFilterParams = {
  search?: string;
  sku?: string;
  skuContains?: string;
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
  ) {}

  async create(dto: CreateProductDto) {
    const sku = await this.resolveSkuForCreate(dto.sku);
    const payload = this.normalizeProductWritePayload({
      ...dto,
      sku,
      isActive: dto.isActive ?? true,
      decimalPoint: dto.decimalPoint ?? MONEY_DECIMAL_PLACES,
    });
    return await this.productModel.create(payload);
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

  async findById(id: string) {
    if (!isValidObjectIdString(id)) throw new NotFoundException('Product not found');
    const doc = await this.productModel.findById(toObjectId(id)).lean();
    if (!doc) throw new NotFoundException('Product not found');
    return doc;
  }

  /** Create or update by SKU. When sku is omitted on create, server allocates next SKU. */
  async upsertBySku(dto: CreateProductDto): Promise<{ created: boolean; product: unknown }> {
    const skuInput = dto.sku?.trim();
    if (skuInput) {
      const existing = await this.findBySku(skuInput);
      if (existing) {
        const { sku: _sku, ...rest } = dto;
        const cleaned = stripInvalidObjectIdRefs(
          { ...rest } as Record<string, unknown>,
          PRODUCT_REF_OBJECT_ID_FIELDS,
        );
        const product = await this.update(String(existing._id), cleaned as UpdateProductDto);
        return { created: false, product };
      }
    }

    const product = await this.create(dto);
    return { created: true, product };
  }

  async update(id: string, dto: UpdateProductDto) {
    if (!isValidObjectIdString(id)) throw new NotFoundException('Product not found');
    const payload = this.normalizeProductWritePayload({ ...dto } as Record<string, unknown>);
    const doc = await this.productModel
      .findByIdAndUpdate(toObjectId(id), { $set: payload }, { new: true })
      .lean();
    if (!doc) throw new NotFoundException('Product not found');
    return doc;
  }

  async list(params: ProductListFilterParams & { skip?: number; limit?: number }) {
    const filter = this.buildProductListFilter(params);
    const skip = params.skip !== undefined ? Math.max(0, params.skip) : 0;
    const limit = params.limit !== undefined ? Math.min(500, Math.max(1, params.limit)) : 200;
    return await this.productModel.find(filter).sort({ updatedAt: -1 }).skip(skip).limit(limit).lean();
  }

  /** Same filter rules as {@link list}, without pagination — for counts. */
  async countForListFilter(params: ProductListFilterParams) {
    return await this.productModel.countDocuments(this.buildProductListFilter(params));
  }

  private buildProductListFilter(params: ProductListFilterParams): FilterQuery<ProductDocument> {
    const filter: FilterQuery<ProductDocument> = {};

    const skuExact = params.sku?.trim();
    const skuPartial = params.skuContains?.trim();
    if (skuExact) {
      filter.sku = skuExact;
    } else if (skuPartial) {
      filter.sku = { $regex: escapeRegex(skuPartial), $options: 'i' };
    }

    const upc = params.upcEanCode?.trim();
    if (upc) filter.upcEanCode = upc;

    const categoryId = params.categoryId?.trim();
    if (categoryId) {
      if (!isValidObjectIdString(categoryId)) {
        throw new BadRequestException('categoryId must be a valid 24-character hex ObjectId');
      }
      filter.categoryId = objectIdRefEquals(categoryId);
    }

    const supplierNameId = params.supplierNameId?.trim();
    if (supplierNameId) {
      if (!isValidObjectIdString(supplierNameId)) {
        throw new BadRequestException('supplierNameId must be a valid 24-character hex ObjectId');
      }
      filter.supplierNameId = objectIdRefEquals(supplierNameId);
    }

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

    const exactMatchFields = [
      'upcEanCode',
      'manufacturerNameId',
      'supplierNameId',
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
      const raw = dto[field];
      if (raw === undefined || raw === null) continue;
      if (typeof raw === 'string' && !isValidObjectIdString(raw)) continue;
      filter[field] = typeof raw === 'string' ? objectIdRefEquals(raw) : raw;
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

    const data = rawData.map((row) =>
      stripInvalidObjectIdRefs({ ...row } as Record<string, unknown>, PRODUCT_REF_OBJECT_ID_FIELDS),
    );
    await this.productModel.populate(
      data,
      PRODUCT_REF_OBJECT_ID_FIELDS.map((path) => ({ path })),
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
    return await this.productModel.find(cursorFilter).sort({ _id: 1 }).limit(limit).lean();
  }
}

