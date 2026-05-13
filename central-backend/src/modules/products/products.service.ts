import { Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model } from 'mongoose';
import { CreateProductDto } from './dto/create-product.dto';
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

  async listDeltas(cursorFilter: Record<string, unknown>, limit: number) {
    return await this.productModel.find(cursorFilter).sort({ _id: 1 }).limit(limit).lean();
  }
}

