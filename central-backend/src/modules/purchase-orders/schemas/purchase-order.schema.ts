import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type PurchaseOrderDocument = HydratedDocument<PurchaseOrder>;

export type PurchaseOrderStatus = 'open' | 'awaiting_approval' | 'approved' | 'partially_received' | 'received' | 'closed';

@Schema({ _id: false })
export class PurchaseOrderSupplierSnapshot {
  @ApiProperty()
  @Prop({ required: true })
  supplierId!: string;

  @ApiProperty({ required: false })
  @Prop()
  code?: string;

  @ApiProperty({ required: false })
  @Prop()
  shortname?: string;

  @ApiProperty({ required: false })
  @Prop()
  name?: string;

  @ApiProperty({ required: false })
  @Prop()
  telPhone?: string;

  @ApiProperty({ required: false })
  @Prop()
  mobile?: string;

  @ApiProperty({ required: false })
  @Prop()
  cashDiscount?: number;
}

@Schema({ _id: false })
export class PurchaseOrderLine {
  @ApiProperty()
  @Prop({ required: true })
  sku!: string;

  @ApiProperty({ required: false })
  @Prop()
  barcode?: string;

  @ApiProperty({ required: false })
  @Prop()
  description?: string;

  @ApiProperty({ required: false })
  @Prop()
  recdQty?: number;

  @ApiProperty({ required: false })
  @Prop()
  freeQty?: number;

  @ApiProperty({ required: false })
  @Prop()
  cost?: number;

  @ApiProperty({ required: false })
  @Prop()
  selling?: number;

  @ApiProperty({ required: false })
  @Prop()
  mrp?: number;

  @ApiProperty({ required: false })
  @Prop()
  discountPercent?: number;

  @ApiProperty({ required: false })
  @Prop()
  discountAmount?: number;

  @ApiProperty({ required: false })
  @Prop()
  taxPercent?: number;

  @ApiProperty({ required: false })
  @Prop()
  taxAmount?: number;

  @ApiProperty({ required: false })
  @Prop()
  cgstPercent?: number;

  @ApiProperty({ required: false })
  @Prop()
  cgstAmount?: number;

  @ApiProperty({ required: false })
  @Prop()
  sgstPercent?: number;

  @ApiProperty({ required: false })
  @Prop()
  sgstAmount?: number;

  @ApiProperty({ required: false })
  @Prop()
  surchargePercent?: number;

  @ApiProperty({ required: false })
  @Prop()
  surchargeAmount?: number;

  @ApiProperty({ required: false })
  @Prop()
  amount?: number;

  @ApiProperty({ required: false })
  @Prop()
  netCost?: number;

  @ApiProperty({ required: false })
  @Prop()
  rotPercent?: number;

  @ApiProperty({ required: false })
  @Prop()
  grossPercent?: number;

  @ApiProperty({ required: false })
  @Prop()
  cashDiscPercent?: number;

  @ApiProperty({ required: false })
  @Prop()
  cashDiscAmount?: number;

  @ApiProperty({ required: false })
  @Prop()
  netAmount?: number;
}

@Schema({ timestamps: true })
export class PurchaseOrder {
  @ApiProperty()
  @Prop({ required: true, unique: true, index: true })
  poNo!: string;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  branchId?: string;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  mainDivisionId?: string;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  mainLocationId?: string;

  @ApiProperty()
  @Prop({ type: PurchaseOrderSupplierSnapshot, required: true })
  supplier!: PurchaseOrderSupplierSnapshot;

  @ApiProperty({ required: false })
  @Prop()
  poDate?: string;

  @ApiProperty({ required: false })
  @Prop()
  deliveryDate?: string;

  @ApiProperty({ required: false })
  @Prop()
  expiryDate?: string;

  @ApiProperty({ required: false })
  @Prop()
  itemDiscAmount?: number;

  @ApiProperty({ required: false })
  @Prop()
  cashDiscPercent?: number;

  @ApiProperty({ required: false })
  @Prop()
  cashDiscount?: number;

  @ApiProperty({ required: false })
  @Prop()
  taxAmount?: number;

  @ApiProperty({ required: false })
  @Prop()
  cgstAmount?: number;

  @ApiProperty({ required: false })
  @Prop()
  sgstAmount?: number;

  @ApiProperty({ required: false })
  @Prop()
  surchargeAmount?: number;

  @ApiProperty({ required: false })
  @Prop()
  netAmount?: number;

  @ApiProperty()
  @Prop({ required: true, default: 'open', index: true })
  status!: PurchaseOrderStatus;

  @ApiProperty({ type: [PurchaseOrderLine] })
  @Prop({ type: [PurchaseOrderLine], default: [] })
  lines!: PurchaseOrderLine[];
}

export const PurchaseOrderSchema = SchemaFactory.createForClass(PurchaseOrder);

