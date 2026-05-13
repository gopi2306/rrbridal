import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type ProductDocument = HydratedDocument<Product>;

@Schema({ timestamps: true })
export class Product {
  // ── Item Information ──

  @ApiProperty()
  @Prop({ required: true, index: true })
  itemName!: string;

  @ApiProperty({ required: false })
  @Prop()
  shortName?: string;

  @ApiProperty({ required: false })
  @Prop()
  alias?: string;

  @ApiProperty()
  @Prop({ required: true, unique: true, index: true })
  sku!: string;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  manufacturerNameId?: string;

  @ApiProperty()
  @Prop({ required: true, index: true })
  supplierNameId!: string;

  // ── Category Information ──

  @ApiProperty({ required: false })
  @Prop()
  itemProductType?: string;

  @ApiProperty()
  @Prop({ required: true, index: true })
  departmentId!: string;

  @ApiProperty()
  @Prop({ required: true, index: true })
  categoryId!: string;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  subCategoryId?: string;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  brandId?: string;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  weightAndSizeId?: string;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  weightPerGmOrMlId?: string;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  offerGroupId?: string;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  productStatusId?: string;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  colourId?: string;

  // ── Tax Information ──

  @ApiProperty({ required: false })
  @Prop({ index: true })
  hsnCodeId?: string;

  @ApiProperty({ required: false })
  @Prop()
  gstCode?: string;

  @ApiProperty({ required: false, description: 'GST percentage (0-100)' })
  @Prop()
  gstPercent?: number;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  gstUomId?: string;

  // ── EAN Code / Barcode ──

  @ApiProperty({ required: false })
  @Prop({ index: true })
  upcEanCode?: string;

  // ── Packing ──

  @ApiProperty({ required: false })
  @Prop()
  subUomConversion?: number;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  uomSubId?: string;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  batchExpiryDetailId?: string;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  itemPrepStatusId?: string;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  packedConfirmationId?: string;

  @ApiProperty({ required: false })
  @Prop()
  grindingCharge?: number;

  @ApiProperty({ required: false })
  @Prop()
  weightGms?: number;

  // ── Item Properties ──

  @ApiProperty({ required: false })
  @Prop()
  decimalPoint?: number;

  @ApiProperty({ required: false })
  @Prop()
  minimumShelfFit?: number;

  // ── Pricing ──

  @ApiProperty({ required: false })
  @Prop({ index: true })
  poQtyPolicyId?: string;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  sellById?: string;

  @ApiProperty({ required: false })
  @Prop()
  itemPerUnit?: number;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  batchSelectionId?: string;

  @ApiProperty({ required: false })
  @Prop()
  itemDiscountAllowed?: boolean;

  @ApiProperty({ required: false })
  @Prop()
  isWeighable?: boolean;

  @ApiProperty({ required: false })
  @Prop()
  unit?: string;

  @ApiProperty({ required: false })
  @Prop()
  costPrice?: number;

  @ApiProperty({ required: false })
  @Prop()
  mrp?: number;

  @ApiProperty({ required: false })
  @Prop()
  sellingPrice?: number;

  @ApiProperty({ required: false })
  @Prop()
  storePrice?: number;

  // ── Reorder Configurations ──

  @ApiProperty({ required: false })
  @Prop({ index: true })
  skuTypeId?: string;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  skuOrderGroupId?: string;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  indentTypeId?: string;

  @ApiProperty({ required: false })
  @Prop()
  minStock?: number;

  @ApiProperty({ required: false })
  @Prop()
  reorderLevel?: number;

  // ── Status ──

  @ApiProperty({ required: false })
  @Prop({ default: true, index: true })
  isActive!: boolean;
}

export const ProductSchema = SchemaFactory.createForClass(Product);
