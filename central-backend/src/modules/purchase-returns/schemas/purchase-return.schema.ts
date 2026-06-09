import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty, OmitType } from '@nestjs/swagger';
import { HydratedDocument, Types } from 'mongoose';

export type PurchaseReturnDocument = HydratedDocument<PurchaseReturn>;

export type PurchaseReturnStatus = 'open' | 'posted' | 'closed';

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
  cashDiscount?: number;
}

@Schema({ _id: false })
export class PurchaseReturnLine {
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

/** API response shape: each line includes populated `product` (not stored on the document). */
export class PurchaseReturnLineResponse extends PurchaseReturnLine {
  @ApiProperty({
    required: false,
    description: 'Full product master with populated refs (department, category, supplier, etc.)',
  })
  product?: Record<string, unknown>;
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

  @ApiProperty({ required: false, description: 'Return document date (same role as poDate on purchase orders)' })
  @Prop()
  purchaseReturnDate?: string;

  @ApiProperty({ required: false })
  @Prop()
  deliveryDate?: string;

  @ApiProperty({ required: false })
  @Prop()
  expiryDate?: string;

  @ApiProperty({ required: false })
  @Prop()
  pucOutSlipNo?: string;

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
  status!: PurchaseReturnStatus;

  @ApiProperty({ type: [PurchaseReturnLineResponse] })
  @Prop({ type: [PurchaseReturnLine], default: [] })
  lines!: PurchaseReturnLine[];
}

/** API response shape: populated masters (not stored on the document). */
export class PurchaseReturnResponse extends OmitType(PurchaseReturn, ['supplier'] as const) {
  @ApiProperty({
    required: false,
    description: 'Embedded supplier copy saved on the purchase return (supplierId, name, code, etc.)',
    type: PurchaseReturnSupplierSnapshot,
  })
  supplierSnapshot?: PurchaseReturnSupplierSnapshot;

  @ApiProperty({
    required: false,
    description: 'Full supplier master document resolved from supplierSnapshot.supplierId',
  })
  supplier?: Record<string, unknown>;

  @ApiProperty({ required: false, description: 'Resolved branch master when branchId is set' })
  branch?: Record<string, unknown>;

  @ApiProperty({ required: false, description: 'Resolved division master when mainDivisionId is set' })
  mainDivision?: Record<string, unknown>;

  @ApiProperty({ required: false, description: 'Resolved location master when mainLocationId is set' })
  mainLocation?: Record<string, unknown>;
}

export const PurchaseReturnSchema = SchemaFactory.createForClass(PurchaseReturn);
