import { Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { DocumentNumberService } from '../../common/document-number.service';
import { DocumentNumberAllocatorService } from '../document-numbers/document-number-allocator.service';
import { DocumentNumberConfigService } from '../document-numbers/document-number-config.service';
import { GoodsReceipt, GoodsReceiptDocument } from './schemas/goods-receipt.schema';

@Injectable()
export class GoodsReceiptNumberGenerator {
  constructor(
    @InjectModel(GoodsReceipt.name) private readonly grModel: Model<GoodsReceiptDocument>,
    private readonly allocator: DocumentNumberAllocatorService,
    private readonly configService: DocumentNumberConfigService,
  ) {}

  async allocateReceiptNoAsync(): Promise<string> {
    const config = await this.configService.getByKey('goods_receipt_rcv');
    return this.allocator.allocate('goods_receipt_rcv', {
      exists: async (v) => !!(await this.grModel.exists({ receiptNo: v }).lean()),
      syncFloorFromValues: () => this.maxSequenceForField('receiptNo', config.prefix),
    });
  }

  async allocateGrnNumberAsync(): Promise<string> {
    const config = await this.configService.getByKey('goods_receipt_grn');
    return this.allocator.allocate('goods_receipt_grn', {
      exists: async (v) => !!(await this.grModel.exists({ grnNumber: v }).lean()),
      syncFloorFromValues: () => this.maxSequenceForField('grnNumber', config.prefix),
    });
  }

  private async maxSequenceForField(
    field: 'receiptNo' | 'grnNumber',
    prefix: string,
  ): Promise<number> {
    const escaped = prefix.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const regex = new RegExp(`^${escaped}\\d+$`, 'i');
    const rows = await this.grModel.find({ [field]: regex }).select(field).lean();

    let max = 0;
    for (const row of rows) {
      const raw = row[field];
      if (typeof raw !== 'string') continue;
      const n = DocumentNumberService.parseSequenceNumber(raw, prefix);
      if (n !== null && n > max) max = n;
    }
    return max;
  }
}
