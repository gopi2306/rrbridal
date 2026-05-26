import { Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { DocumentNumberService } from '../../common/document-number.service';
import { DocumentNumberAllocatorService } from '../document-numbers/document-number-allocator.service';
import { DocumentNumberConfigService } from '../document-numbers/document-number-config.service';
import { Product, ProductDocument } from './schemas/product.schema';

@Injectable()
export class ProductSkuGenerator {
  constructor(
    @InjectModel(Product.name) private readonly productModel: Model<ProductDocument>,
    private readonly allocator: DocumentNumberAllocatorService,
    private readonly configService: DocumentNumberConfigService,
  ) {}

  /** Allocates the next unique SKU using configured prefix/pad/startFrom. */
  async allocateNextAsync(): Promise<string> {
    const config = await this.configService.getByKey('product_sku');
    const prefix = config.prefix;

    return this.allocator.allocate('product_sku', {
      exists: async (v) => !!(await this.productModel.exists({ sku: v }).lean()),
      syncFloorFromValues: () => this.maxSequenceForPrefix(prefix),
    });
  }

  private async maxSequenceForPrefix(prefix: string): Promise<number> {
    const escaped = prefix.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const regex = new RegExp(`^${escaped}\\d+$`, 'i');
    const rows = await this.productModel.find({ sku: regex }).select('sku').lean();

    let max = 0;
    for (const row of rows) {
      if (typeof row.sku !== 'string') continue;
      const n = DocumentNumberService.parseSequenceNumber(row.sku, prefix);
      if (n !== null && n > max) max = n;
    }
    return max;
  }
}
