import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument, Types } from 'mongoose';

export type GrnAutoTransferHistoryStatus = 'awaiting_intake' | 'completed' | 'cancelled';

export type GrnAutoTransferHistoryDocument = HydratedDocument<GrnAutoTransferHistory>;

@Schema({ timestamps: true, collection: 'grn_auto_transfer_histories' })
export class GrnAutoTransferHistory {
  @ApiProperty({ description: 'Source posted goods receipt' })
  @Prop({ type: Types.ObjectId, ref: 'GoodsReceipt', required: true, index: true })
  goodsReceiptId!: Types.ObjectId;

  @ApiProperty({ description: 'Linked stock transfer document' })
  @Prop({ type: Types.ObjectId, ref: 'StockTransfer', required: true, index: true })
  stockTransferId!: Types.ObjectId;

  @ApiProperty()
  @Prop({ required: true, trim: true })
  transferNo!: string;

  @ApiProperty({ description: 'GRN number or receipt number for display/errors' })
  @Prop({ required: true, trim: true })
  grnLabel!: string;

  @ApiProperty()
  @Prop({ required: true, trim: true, index: true })
  toStoreId!: string;

  @ApiProperty({ enum: ['awaiting_intake', 'completed', 'cancelled'] })
  @Prop({ required: true, default: 'awaiting_intake', index: true })
  status!: GrnAutoTransferHistoryStatus;

  @ApiProperty({ required: false })
  @Prop({ type: Types.ObjectId })
  fromLocationId?: Types.ObjectId;

  @ApiProperty({ required: false })
  @Prop({ trim: true })
  receivedBy?: string;

  @ApiProperty({ required: false })
  @Prop({ trim: true })
  remarks?: string;

  @ApiProperty({ required: false })
  @Prop()
  completedAt?: Date;

  @ApiProperty({ required: false })
  @Prop()
  cancelledAt?: Date;
}

export const GrnAutoTransferHistorySchema = SchemaFactory.createForClass(GrnAutoTransferHistory);

GrnAutoTransferHistorySchema.index(
  { goodsReceiptId: 1 },
  {
    unique: true,
    partialFilterExpression: {
      status: { $in: ['awaiting_intake', 'completed'] },
    },
  },
);
