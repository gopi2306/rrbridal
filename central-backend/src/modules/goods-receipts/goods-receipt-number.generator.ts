import { Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { DocumentNumberService } from '../../common/document-number.service';
import { GoodsReceipt, GoodsReceiptDocument } from './schemas/goods-receipt.schema';

const RECEIPT_PREFIX = 'RCV-';
const GRN_PREFIX = 'GRN-';
const NUMBER_PAD = 6;

@Injectable()
export class GoodsReceiptNumberGenerator {
  constructor(
    @InjectModel(GoodsReceipt.name) private readonly grModel: Model<GoodsReceiptDocument>,
    private readonly documentNumbers: DocumentNumberService,
  ) {}

  allocateReceiptNoAsync(): Promise<string> {
    return this.documentNumbers.allocateNext({
      sequenceKey: 'goods_receipt_receipt_no',
      prefix: RECEIPT_PREFIX,
      pad: NUMBER_PAD,
      exists: async (v) => !!(await this.grModel.exists({ receiptNo: v }).lean()),
      syncFloorFromValues: () => this.maxSequenceForField('receiptNo', RECEIPT_PREFIX),
    });
  }

  allocateGrnNumberAsync(): Promise<string> {
    return this.documentNumbers.allocateNext({
      sequenceKey: 'goods_receipt_grn',
      prefix: GRN_PREFIX,
      pad: NUMBER_PAD,
      exists: async (v) => !!(await this.grModel.exists({ grnNumber: v }).lean()),
      syncFloorFromValues: () => this.maxSequenceForField('grnNumber', GRN_PREFIX),
    });
  }

  private async maxSequenceForField(
    field: 'receiptNo' | 'grnNumber',
    prefix: string,
  ): Promise<number> {
    const regex = new RegExp(`^${prefix.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}\\d+$`, 'i');
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
