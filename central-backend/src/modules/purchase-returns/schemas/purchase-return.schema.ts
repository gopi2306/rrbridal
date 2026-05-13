import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type PurchaseReturnDocument = HydratedDocument<PurchaseReturn>;

@Schema({ _id: false })
export class PurchaseReturnSupplierSnapshot {
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
  cashDiscPercent?: number;
}

@Schema({ _id: false })
export class PurchaseReturnLine {
  @ApiProperty()
  @Prop({ required: true })
  sku!: string;

  @ApiProperty({ required: false })
  @Prop()
  description?: string;

  @ApiProperty({ required: false })
  @Prop()
  qty?: number;

  @ApiProperty({ required: false })
  @Prop()
  freeQty?: number;

  @ApiProperty({ required: false })
  @Prop()
  cost?: number;

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
}

@Schema({ timestamps: true })
export class PurchaseReturn {
  @ApiProperty()
  @Prop({ required: true, unique: true, index: true })
  purchaseReturnNo!: string;

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
  @Prop({ type: PurchaseReturnSupplierSnapshot, required: true })
  supplier!: PurchaseReturnSupplierSnapshot;

  @ApiProperty({ required: false })
  @Prop()
  purchaseReturnDate?: string;

  @ApiProperty({ required: false })
  @Prop()
  pucOutSlipNo?: string;

  @ApiProperty({ required: false })
  @Prop()
  itemDiscAmount?: number;

  @ApiProperty({ required: false })
  @Prop()
  cashDiscAmount?: number;

  @ApiProperty({ required: false })
  @Prop()
  netAmount?: number;

  @ApiProperty({ type: [PurchaseReturnLine] })
  @Prop({ type: [PurchaseReturnLine], default: [] })
  lines!: PurchaseReturnLine[];
}

export const PurchaseReturnSchema = SchemaFactory.createForClass(PurchaseReturn);

