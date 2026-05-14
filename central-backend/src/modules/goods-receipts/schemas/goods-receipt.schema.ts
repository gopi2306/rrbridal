import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type GoodsReceiptDocument = HydratedDocument<GoodsReceipt>;

export type GoodsReceiptStatus = 'draft' | 'posted';
export type GoodsReceiptLineOutcome = 'valid' | 'invalid' | 'damaged';

@Schema({ _id: false })
export class GoodsReceiptSupplierSnapshot {
  @ApiProperty()
  @Prop({ required: true })
  supplierId!: string;

  @ApiProperty({ required: false })
  @Prop()
  name?: string;
}

@Schema({ _id: false })
export class GoodsReceiptLine {
  @ApiProperty()
  @Prop({ required: true })
  sku!: string;

  @ApiProperty({ required: false })
  @Prop()
  description?: string;

  @ApiProperty({ required: false })
  @Prop()
  orderedQty?: number;

  @ApiProperty({ required: false })
  @Prop()
  receivedQty?: number;

  @ApiProperty({ required: false, enum: ['valid', 'invalid', 'damaged'] })
  @Prop({ default: 'valid' })
  outcome?: GoodsReceiptLineOutcome;
}

@Schema({ timestamps: true })
export class GoodsReceipt {
  @ApiProperty()
  @Prop({ required: true, unique: true, index: true })
  receiptNo!: string; // e.g. RCV-032

  @ApiProperty({ required: false })
  @Prop({ index: true })
  poId?: string;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  poNo?: string;

  @ApiProperty({ required: false, description: 'GRN number' })
  @Prop({ index: true })
  grnNumber?: string;

  @ApiProperty({ required: false })
  @Prop({ type: GoodsReceiptSupplierSnapshot })
  supplier?: GoodsReceiptSupplierSnapshot;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  invoiceNo?: string;

  @ApiProperty({ required: false })
  @Prop()
  invoiceDate?: string; // dd/mm/yyyy as per UI, can normalize later

  @ApiProperty({ required: false })
  @Prop()
  remarks?: string;

  @ApiProperty({ required: false, default: 'draft', enum: ['draft', 'posted'] })
  @Prop({ required: true, default: 'draft', index: true })
  status!: GoodsReceiptStatus;

  @ApiProperty({ type: [GoodsReceiptLine] })
  @Prop({ type: [GoodsReceiptLine], default: [] })
  lines!: GoodsReceiptLine[];
}

export const GoodsReceiptSchema = SchemaFactory.createForClass(GoodsReceipt);

