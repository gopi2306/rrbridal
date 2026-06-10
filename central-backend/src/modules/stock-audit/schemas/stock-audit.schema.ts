import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument, Types } from 'mongoose';

export type StockAuditDocument = HydratedDocument<StockAudit>;

export type StockAuditStatus = 'draft' | 'in_progress' | 'completed' | 'cancelled';

@Schema({ _id: false })
export class StockAuditLine {
  @ApiProperty({
    required: false,
    description: 'Mongo ObjectId of the product master; resolved from sku on save when omitted',
  })
  @Prop({ type: Types.ObjectId, ref: 'Product', index: true })
  productId?: Types.ObjectId;

  @ApiProperty()
  @Prop({ required: true })
  sku!: string;

  @ApiProperty({ description: 'Book / expected on-hand quantity at audit start' })
  @Prop({ required: true, default: 0 })
  orderedQty!: number;

  @ApiProperty({ description: 'Physically scanned quantity during audit' })
  @Prop({ required: true, default: 0 })
  scannedQty!: number;
}

@Schema({ timestamps: true, collection: 'stock_audits' })
export class StockAudit {
  @ApiProperty({ example: 'SA-000001' })
  @Prop({ required: true, unique: true, index: true })
  auditNo!: string;

  @ApiProperty({ example: 'store-001' })
  @Prop({ required: true, index: true })
  storeId!: string;

  @ApiProperty({ enum: ['draft', 'in_progress', 'completed', 'cancelled'], default: 'in_progress' })
  @Prop({ required: true, default: 'in_progress', index: true })
  status!: StockAuditStatus;

  @ApiProperty({ type: [StockAuditLine] })
  @Prop({ type: [StockAuditLine], default: [] })
  lines!: StockAuditLine[];
}

export const StockAuditSchema = SchemaFactory.createForClass(StockAudit);

StockAuditSchema.index({ storeId: 1, status: 1 });
