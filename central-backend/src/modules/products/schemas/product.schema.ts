import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument, Types } from 'mongoose';

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
  @Prop({ type: Types.ObjectId, ref: 'Manufacturer', index: true })
  manufacturerNameId?: Types.ObjectId;

  @ApiProperty()
  @Prop({ type: Types.ObjectId, ref: 'Supplier', required: true, index: true })
  supplierNameId!: Types.ObjectId;

  // ── Category Information ──

  @ApiProperty({ required: false })
  @Prop()
  itemProductType?: string;

  @ApiProperty()
  @Prop({ type: Types.ObjectId, ref: 'Department', required: true, index: true })
  departmentId!: Types.ObjectId;

  @ApiProperty()
  @Prop({ type: Types.ObjectId, ref: 'Category', required: true, index: true })
  categoryId!: Types.ObjectId;

  @ApiProperty({ required: false })
  @Prop({ type: Types.ObjectId, ref: 'SubCategory', index: true })
  subCategoryId?: Types.ObjectId;

  @ApiProperty({ required: false })
  @Prop({ type: Types.ObjectId, ref: 'Brand', index: true })
  brandId?: Types.ObjectId;

  @ApiProperty({ required: false })
  @Prop({ type: Types.ObjectId, ref: 'WeightSize', index: true })
  weightAndSizeId?: Types.ObjectId;

  @ApiProperty({ required: false })
  @Prop({ type: Types.ObjectId, ref: 'WeightUnit', index: true })
  weightPerGmOrMlId?: Types.ObjectId;

  @ApiProperty({ required: false })
  @Prop({ type: Types.ObjectId, ref: 'OfferGroup', index: true })
  offerGroupId?: Types.ObjectId;

  @ApiProperty({ required: false })
  @Prop({ type: Types.ObjectId, ref: 'ProductStatus', index: true })
  productStatusId?: Types.ObjectId;

  @ApiProperty({ required: false })
  @Prop({ type: Types.ObjectId, ref: 'Colour', index: true })
  colourId?: Types.ObjectId;

  // ── Tax Information ──

  @ApiProperty({ required: false })
  @Prop({ type: Types.ObjectId, ref: 'HsnCode', index: true })
  hsnCodeId?: Types.ObjectId;

  @ApiProperty({ required: false })
  @Prop()
  gstCode?: string;

  @ApiProperty({ required: false, description: 'GST percentage (0-100)' })
  @Prop()
  gstPercent?: number;

  @ApiProperty({ required: false })
  @Prop({ type: Types.ObjectId, ref: 'GstUom', index: true })
  gstUomId?: Types.ObjectId;

  // ── EAN Code / Barcode ──

  @ApiProperty({ required: false })
  @Prop({ index: true })
  upcEanCode?: string;

  // ── Packing ──

  @ApiProperty({ required: false })
  @Prop()
  subUomConversion?: number;

  @ApiProperty({ required: false })
  @Prop({ type: Types.ObjectId, ref: 'UomSub', index: true })
  uomSubId?: Types.ObjectId;

  @ApiProperty({ required: false })
  @Prop({ type: Types.ObjectId, ref: 'BatchExpiryDetail', index: true })
  batchExpiryDetailId?: Types.ObjectId;

  @ApiProperty({ required: false })
  @Prop({ type: Types.ObjectId, ref: 'ItemPrepStatus', index: true })
  itemPrepStatusId?: Types.ObjectId;

  @ApiProperty({ required: false })
  @Prop({ type: Types.ObjectId, ref: 'PackedConfirmation', index: true })
  packedConfirmationId?: Types.ObjectId;

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
  @Prop({ type: Types.ObjectId, ref: 'PoQtyPolicy', index: true })
  poQtyPolicyId?: Types.ObjectId;

  @ApiProperty({ required: false })
  @Prop({ type: Types.ObjectId, ref: 'SellByType', index: true })
  sellById?: Types.ObjectId;

  @ApiProperty({ required: false })
  @Prop()
  itemPerUnit?: number;

  @ApiProperty({ required: false })
  @Prop({ type: Types.ObjectId, ref: 'BatchSelection', index: true })
  batchSelectionId?: Types.ObjectId;

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
  @Prop({ type: Types.ObjectId, ref: 'SkuType', index: true })
  skuTypeId?: Types.ObjectId;

  @ApiProperty({ required: false })
  @Prop({ type: Types.ObjectId, ref: 'SkuOrderGroup', index: true })
  skuOrderGroupId?: Types.ObjectId;

  @ApiProperty({ required: false })
  @Prop({ type: Types.ObjectId, ref: 'IndentType', index: true })
  indentTypeId?: Types.ObjectId;

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
