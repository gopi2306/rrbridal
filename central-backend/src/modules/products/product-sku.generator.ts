import { Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { IdSequence, IdSequenceDocument } from './schemas/id-sequence.schema';
import { Product, ProductDocument } from './schemas/product.schema';

const SEQUENCE_KEY = 'product_sku';
const SKU_PREFIX = 'SKU-';
const SKU_PAD = 6;

function parseSkuSequenceNumber(sku: string): number | null {
  const m = /^SKU-(\d+)$/i.exec(sku.trim());
  const digits = m?.[1];
  return digits ? parseInt(digits, 10) : null;
}

@Injectable()
export class ProductSkuGenerator {
  constructor(
    @InjectModel(IdSequence.name) private readonly sequenceModel: Model<IdSequenceDocument>,
    @InjectModel(Product.name) private readonly productModel: Model<ProductDocument>,
  ) {}

  /** Allocates the next unique SKU (e.g. SKU-000042). */
  async allocateNextAsync(): Promise<string> {
    await this.syncSequenceFloorFromExistingProducts();

    for (let attempt = 0; attempt < 5; attempt++) {
      const doc = await this.sequenceModel
        .findOneAndUpdate(
          { _id: SEQUENCE_KEY },
          { $inc: { seq: 1 } },
          { upsert: true, new: true, setDefaultsOnInsert: true },
        )
        .lean();

      const seq = typeof doc?.seq === 'number' ? doc.seq : 1;
      const sku = `${SKU_PREFIX}${String(seq).padStart(SKU_PAD, '0')}`;

      const taken = await this.productModel.exists({ sku }).lean();
      if (!taken) return sku;
    }

    return `${SKU_PREFIX}${Date.now()}`;
  }

  /** Ensures the counter is at least the highest existing SKU-###### in products. */
  private async syncSequenceFloorFromExistingProducts() {
    const rows = await this.productModel
      .find({ sku: { $regex: `^${SKU_PREFIX}\\d+$`, $options: 'i' } })
      .select('sku')
      .lean();

    let max = 0;
    for (const row of rows) {
      if (typeof row.sku !== 'string') continue;
      const n = parseSkuSequenceNumber(row.sku);
      if (n !== null && n > max) max = n;
    }

    if (max > 0) {
      await this.sequenceModel.updateOne(
        { _id: SEQUENCE_KEY },
        { $max: { seq: max } },
        { upsert: true },
      );
    }
  }
}
