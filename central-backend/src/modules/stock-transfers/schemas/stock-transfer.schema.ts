import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument, Types } from 'mongoose';

export type StockTransferDocument = HydratedDocument<StockTransfer>;

export type StockTransferStatus = 'draft' | 'in_transit' | 'awaiting_intake' | 'completed' | 'cancelled';

export type StockTransferDirection = 'warehouse_to_store' | 'store_to_warehouse';

export type StockTransferFromKind = 'warehouse' | 'store';

@Schema({ _id: false })
export class StockTransferLine {
  @ApiProperty({
    required: false,
    description: 'Mongo ObjectId of the product master; resolved from sku on save when omitted',
  })
  @Prop({ type: Types.ObjectId, ref: 'Product', index: true })
  productId?: Types.ObjectId;

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

/** API response shape: each line includes populated `product` (not stored on the document). */
export class StockTransferLineResponse extends StockTransferLine {
  @ApiProperty({
    required: false,
    description: 'Full product master with populated refs (department, category, supplier, etc.)',
  })
  product?: Record<string, unknown>;
}

@Schema({ timestamps: true, collection: 'stock_transfers' })
export class StockTransfer {
  @ApiProperty()
  @Prop({ required: true, unique: true, index: true })
  transferNo!: string;

  @ApiProperty({
    enum: ['warehouse_to_store', 'store_to_warehouse'],
    default: 'warehouse_to_store',
  })
  @Prop({ required: true, default: 'warehouse_to_store', index: true })
  direction!: StockTransferDirection;

  @ApiProperty({ enum: ['warehouse', 'store'] })
  @Prop({ required: true, default: 'warehouse' })
  fromKind!: StockTransferFromKind;

  @ApiProperty({
    required: false,
    description: 'Mongo _id of the source warehouse Location (transfer in)',
  })
  @Prop({ type: Types.ObjectId, index: true })
  fromLocationId?: Types.ObjectId;

  @ApiProperty({
    required: false,
    description: 'Source store id (transfer out)',
  })
  @Prop({ index: true })
  fromStoreId?: string;

  @ApiProperty({
    required: false,
    description: 'Destination store id (transfer in)',
  })
  @Prop({ index: true })
  toStoreId?: string;

  @ApiProperty({
    required: false,
    description: 'Mongo _id of the destination warehouse Location (transfer out)',
  })
  @Prop({ type: Types.ObjectId, index: true })
  toLocationId?: Types.ObjectId;

  @ApiProperty({ required: false })
  @Prop({ type: Types.ObjectId, index: true })
  purchaseIntentId?: Types.ObjectId;

  @ApiProperty({
    required: false,
    description: 'Mongo _id of the source goods receipt (GRN) when created via from-grn',
  })
  @Prop({ type: Types.ObjectId, ref: 'GoodsReceipt', index: true })
  goodsReceiptId?: Types.ObjectId;

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

  @ApiProperty({
    required: false,
    description: 'ISO timestamp when the store confirmed physical receipt or dispatch',
  })
  @Prop()
  receivedAt?: string;

  @ApiProperty({
    required: false,
    description: 'Store operator name or user id who confirmed receipt',
  })
  @Prop({ trim: true })
  receivedBy?: string;

  @ApiProperty({ type: [StockTransferLineResponse] })
  @Prop({ type: [StockTransferLine], default: [] })
  lines!: StockTransferLine[];
}

export const StockTransferSchema = SchemaFactory.createForClass(StockTransfer);

StockTransferSchema.index({ direction: 1, toStoreId: 1, status: 1 });
StockTransferSchema.index({ direction: 1, fromStoreId: 1, status: 1 });
