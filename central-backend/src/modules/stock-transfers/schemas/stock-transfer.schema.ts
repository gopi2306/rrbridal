import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument, Types } from 'mongoose';

export type StockTransferDocument = HydratedDocument<StockTransfer>;

export type StockTransferStatus = 'draft' | 'in_transit' | 'awaiting_intake' | 'completed' | 'cancelled';

@Schema({ _id: false })
export class StockTransferLine {
  @ApiProperty()
  @Prop({ required: true })
  sku!: string;

  @ApiProperty({ required: false })
  @Prop()
  description?: string;

  @ApiProperty()
  @Prop({ required: true })
  qty!: number;
}

@Schema({ timestamps: true, collection: 'stock_transfers' })
export class StockTransfer {
  @ApiProperty()
  @Prop({ required: true, unique: true, index: true })
  transferNo!: string;

  @ApiProperty({ enum: ['warehouse'] })
  @Prop({ required: true, default: 'warehouse' })
  fromKind!: 'warehouse';

  @ApiProperty({
    required: false,
    description: 'Mongo _id of the source warehouse Location (type warehouse, active)',
  })
  @Prop({ type: Types.ObjectId, index: true })
  fromLocationId?: Types.ObjectId;

  @ApiProperty()
  @Prop({ required: true, index: true })
  toStoreId!: string;

  @ApiProperty({ required: false })
  @Prop({ type: Types.ObjectId, index: true })
  purchaseIntentId?: Types.ObjectId;

  @ApiProperty()
  @Prop({ required: true, default: 'draft', index: true })
  status!: StockTransferStatus;

  @ApiProperty({ required: false })
  @Prop()
  transferDate?: string;

  @ApiProperty({ required: false })
  @Prop()
  remarks?: string;

  @ApiProperty({
    required: false,
    description: 'Stock classification label (e.g. Normal Stock); defaults on create when omitted',
    example: 'Normal Stock',
  })
  @Prop({ trim: true })
  stockClassification?: string;

  @ApiProperty({ type: [StockTransferLine] })
  @Prop({ type: [StockTransferLine], default: [] })
  lines!: StockTransferLine[];
}

export const StockTransferSchema = SchemaFactory.createForClass(StockTransfer);
