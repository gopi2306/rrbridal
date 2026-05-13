import { Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model, SortOrder } from 'mongoose';
import { CreateProductDto } from './dto/create-product.dto';
import { FilterProductDto } from './dto/filter-product.dto';
import { UpdateProductDto } from './dto/update-product.dto';
import { Product, ProductDocument } from './schemas/product.schema';

@Injectable()
export class ProductsService {
  constructor(@InjectModel(Product.name) private readonly productModel: Model<ProductDocument>) {}

  async create(dto: CreateProductDto) {
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

  async list(params: { search?: string; sku?: string; upcEanCode?: string; categoryId?: string; supplierNameId?: string }) {
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

    return await this.productModel.find(filter).sort({ updatedAt: -1 }).limit(200).lean();
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

