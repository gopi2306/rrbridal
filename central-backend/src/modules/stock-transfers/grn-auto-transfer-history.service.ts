import { BadRequestException, Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model, Types } from 'mongoose';
import {
  GrnAutoTransferHistory,
  GrnAutoTransferHistoryDocument,
  GrnAutoTransferHistoryStatus,
} from './schemas/grn-auto-transfer-history.schema';

export type CreateGrnAutoTransferHistoryInput = {
  goodsReceiptId: string;
  stockTransferId: string;
  transferNo: string;
  grnLabel: string;
  toStoreId: string;
  status: GrnAutoTransferHistoryStatus;
  fromLocationId?: Types.ObjectId;
  receivedBy?: string;
  remarks?: string;
};

@Injectable()
export class GrnAutoTransferHistoryService {
  constructor(
    @InjectModel(GrnAutoTransferHistory.name)
    private readonly model: Model<GrnAutoTransferHistoryDocument>,
  ) {}

  async findBlockingByGrnId(grnId: string): Promise<GrnAutoTransferHistoryDocument | null> {
    if (!Types.ObjectId.isValid(grnId)) return null;
    return await this.model.findOne({
      goodsReceiptId: new Types.ObjectId(grnId),
      status: { $in: ['awaiting_intake', 'completed'] },
    });
  }

  assertNotBlocked(
    existing: GrnAutoTransferHistoryDocument | null,
    grnLabel: string,
  ): void {
    if (!existing) return;

    if (existing.status === 'completed') {
      throw new BadRequestException(
        `Auto transfer already completed for GRN ${grnLabel}. Transfer ${existing.transferNo} was sent to store ${existing.toStoreId}.`,
      );
    }

    if (existing.status === 'awaiting_intake') {
      throw new BadRequestException(
        `Auto transfer already in progress for GRN ${grnLabel}. Transfer ${existing.transferNo} is awaiting store intake at ${existing.toStoreId}.`,
      );
    }
  }

  async createRecord(input: CreateGrnAutoTransferHistoryInput): Promise<GrnAutoTransferHistoryDocument> {
    try {
      return await this.model.create({
        goodsReceiptId: new Types.ObjectId(input.goodsReceiptId),
        stockTransferId: new Types.ObjectId(input.stockTransferId),
        transferNo: input.transferNo,
        grnLabel: input.grnLabel,
        toStoreId: input.toStoreId,
        status: input.status,
        ...(input.fromLocationId ? { fromLocationId: input.fromLocationId } : {}),
        ...(input.receivedBy ? { receivedBy: input.receivedBy } : {}),
        ...(input.remarks ? { remarks: input.remarks } : {}),
      });
    } catch (err: unknown) {
      const dup =
        err && typeof err === 'object' && 'code' in err && (err as { code?: number }).code === 11000;
      if (dup) {
        const blocking = await this.findBlockingByGrnId(input.goodsReceiptId);
        this.assertNotBlocked(blocking, input.grnLabel);
      }
      throw err;
    }
  }

  async markCompleted(stockTransferId: string): Promise<void> {
    if (!Types.ObjectId.isValid(stockTransferId)) return;
    await this.model.updateOne(
      { stockTransferId: new Types.ObjectId(stockTransferId), status: 'awaiting_intake' },
      { $set: { status: 'completed', completedAt: new Date() } },
    );
  }

  async markCancelled(stockTransferId: string): Promise<void> {
    if (!Types.ObjectId.isValid(stockTransferId)) return;
    await this.model.updateOne(
      {
        stockTransferId: new Types.ObjectId(stockTransferId),
        status: { $in: ['awaiting_intake', 'completed'] },
      },
      { $set: { status: 'cancelled', cancelledAt: new Date() } },
    );
  }
}
