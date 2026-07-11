import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type InventoryAdjustmentDocument = HydratedDocument<InventoryAdjustment>;

export type InventoryAdjustmentLocationKind = 'store' | 'warehouse';
export type InventoryAdjustmentSource = 'central_admin' | 'wpf_sync';
export type InventoryAdjustmentStatus = 'posted';

@Schema({ _id: false })
export class InventoryAdjustmentLine {
  @ApiProperty()
  @Prop({ required: true })
  sku!: string;

  @ApiProperty()
  @Prop({ required: true })
  qtyBefore!: number;

  @ApiProperty()
  @Prop({ required: true })
  qtyDelta!: number;

  @ApiProperty()
  @Prop({ required: true })
  qtyAfter!: number;

  @ApiProperty({ required: false })
  @Prop()
  note?: string;
}

@Schema({ timestamps: true, collection: 'inventory_adjustments' })
export class InventoryAdjustment {
  @ApiProperty({ example: 'IA-000001' })
  @Prop({ required: true, unique: true, index: true })
  adjustmentNo!: string;

  @ApiProperty({ enum: ['store', 'warehouse'] })
  @Prop({ required: true, index: true })
  locationKind!: InventoryAdjustmentLocationKind;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  storeId?: string;

  @ApiProperty({ required: false })
  @Prop({ trim: true, lowercase: true, index: true })
  locationCode?: string;

  @ApiProperty({ enum: ['central_admin', 'wpf_sync'] })
  @Prop({ required: true, index: true })
  source!: InventoryAdjustmentSource;

  @ApiProperty({ required: false })
  @Prop({ unique: true, sparse: true, index: true })
  sourceEventId?: string;

  @ApiProperty({ required: false })
  @Prop()
  deviceId?: string;

  @ApiProperty()
  @Prop({ required: true })
  reason!: string;

  @ApiProperty({ enum: ['posted'], default: 'posted' })
  @Prop({ required: true, default: 'posted', index: true })
  status!: InventoryAdjustmentStatus;

  @ApiProperty({ type: [InventoryAdjustmentLine] })
  @Prop({ type: [InventoryAdjustmentLine], default: [] })
  lines!: InventoryAdjustmentLine[];
}

export const InventoryAdjustmentSchema = SchemaFactory.createForClass(InventoryAdjustment);

InventoryAdjustmentSchema.index({ storeId: 1, status: 1, _id: 1 });
InventoryAdjustmentSchema.index({ locationCode: 1, status: 1, _id: 1 });
