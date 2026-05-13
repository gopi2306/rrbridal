import { Inject, Injectable, NotFoundException, forwardRef } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model, SortOrder } from 'mongoose';
import { ResourceLimitsService } from '../resource-limits/resource-limits.service';
import { CreateProductDto } from './dto/create-product.dto';
import { FilterProductDto } from './dto/filter-product.dto';
import { UpdateProductDto } from './dto/update-product.dto';
import { Product, ProductDocument } from './schemas/product.schema';

@Injectable()
export class ProductsService {
  constructor(
    @InjectModel(Product.name) private readonly productModel: Model<ProductDocument>,
    @Inject(forwardRef(() => ResourceLimitsService)) private readonly resourceLimits: ResourceLimitsService,
  ) {}

  async create(dto: CreateProductDto) {
    const willBeActive = dto.isActive ?? true;
    if (willBeActive) {
      await this.resourceLimits.assertProductLimit();
    }
    return await this.productModel.create({
      ...dto,
      isActive: dto.isActive ?? true,
    });
  }

  async findById(id: string) {
    const doc = await this.productModel.findById(id).lean();
    if (!doc) throw new NotFoundException('Product not found');
    return doc;
  }

  async update(id: string, dto: UpdateProductDto) {
    const doc = await this.productModel.findByIdAndUpdate(id, { $set: dto }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Product not found');
    return doc;
  }

  async list(params: {
    search?: string;
    sku?: string;
    upcEanCode?: string;
    categoryId?: string;
    supplierNameId?: string;
    skip?: number;
    limit?: number;
  }) {
    const filter = this.buildProductListFilter(params);
    const skip = params.skip !== undefined ? Math.max(0, params.skip) : 0;
    const limit = params.limit !== undefined ? Math.min(500, Math.max(1, params.limit)) : 200;
    return await this.productModel.find(filter).sort({ updatedAt: -1 }).skip(skip).limit(limit).lean();
  }

  /** Same filter rules as {@link list}, without pagination — for counts. */
  async countForListFilter(params: {
    search?: string;
    sku?: string;
    upcEanCode?: string;
    categoryId?: string;
    supplierNameId?: string;
  }) {
    return await this.productModel.countDocuments(this.buildProductListFilter(params));
  }

  private buildProductListFilter(params: {
    search?: string;
    sku?: string;
    upcEanCode?: string;
    categoryId?: string;
    supplierNameId?: string;
  }): FilterQuery<ProductDocument> {
    const filter: FilterQuery<ProductDocument> = {};

    if (params.sku) filter.sku = params.sku;
    if (params.upcEanCode) filter.upcEanCode = params.upcEanCode;
    if (params.categoryId) filter.categoryId = params.categoryId;
    if (params.supplierNameId) filter.supplierNameId = params.supplierNameId;

    if (params.search) {
      filter.$or = [
        { itemName: { $regex: params.search, $options: 'i' } },
        { shortName: { $regex: params.search, $options: 'i' } },
        { alias: { $regex: params.search, $options: 'i' } },
        { sku: { $regex: params.search, $options: 'i' } },
        { upcEanCode: { $regex: params.search, $options: 'i' } },
      ];
    }

    return filter;
  }

  async filter(dto: FilterProductDto) {
    const filter: FilterQuery<ProductDocument> = {};

    const exactMatchFields = [
      'sku',
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
      if (dto[field] !== undefined && dto[field] !== null) {
        filter[field] = dto[field];
      }
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

    if (dto.search) {
      filter.$or = [
        { itemName: { $regex: dto.search, $options: 'i' } },
        { shortName: { $regex: dto.search, $options: 'i' } },
        { alias: { $regex: dto.search, $options: 'i' } },
        { sku: { $regex: dto.search, $options: 'i' } },
        { upcEanCode: { $regex: dto.search, $options: 'i' } },
      ];
    }

    const page = dto.page ?? 1;
    const limit = dto.limit ?? 20;
    const skip = (page - 1) * limit;
    const sortBy = dto.sortBy ?? 'updatedAt';
    const sortOrder: SortOrder = dto.sortOrder === 'asc' ? 1 : -1;

    const [data, total] = await Promise.all([
      this.productModel
        .find(filter)
        .populate([
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
        ])
        .sort({ [sortBy]: sortOrder })
        .skip(skip)
        .limit(limit)
        .lean(),
      this.productModel.countDocuments(filter),
    ]);

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

