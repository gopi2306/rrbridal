import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';
import type {
  BarcodeDecoration,
  BarcodeFieldStyleKey,
  BarcodeFontWeight,
  BarcodeHumanTextStyle,
  BarcodeLabelBarcodeSettings,
  BarcodeLabelFields,
  BarcodeLabelPrintOffsetMm,
  BarcodeLabelTextSettings,
  BarcodeLayoutStyle,
  BarcodePriceStyle,
  BarcodeProductNameSource,
  BarcodeTextAlignment,
} from '../barcode-label-design.types';

export type BarcodeLabelDesignDocument = HydratedDocument<BarcodeLabelDesign>;

@Schema({ _id: false })
export class BarcodeLabelFieldStyleSchema {
  @Prop({ required: true, min: 1 })
  sizePt!: number;

  @Prop({ required: true, enum: ['regular', 'bold'] })
  weight!: BarcodeFontWeight;
}

@Schema({ _id: false })
export class BarcodeLabelFieldsSchema implements BarcodeLabelFields {
  @Prop({ default: true })
  productName!: boolean;

  @Prop({ default: true })
  designSku!: boolean;

  @Prop({ default: true })
  sellingPrice!: boolean;

  @Prop({ default: true })
  sizeNote!: boolean;

  @Prop({ default: false })
  batchNumber!: boolean;

  @Prop({ default: false })
  expiryDate!: boolean;

  @Prop({ default: false })
  brandName!: boolean;
}

@Schema({ _id: false })
export class BarcodeLabelTextSettingsSchema implements BarcodeLabelTextSettings {
  @Prop({ enum: ['itemName', 'shortName', 'alias'], default: 'itemName' })
  productNameSource!: BarcodeProductNameSource;

  @Prop({ trim: true, default: 'D.No:' })
  designNoPrefix!: string;

  @Prop({ trim: true, default: 'Price ₹:' })
  pricePrefix!: string;

  @Prop({ trim: true, default: 'Note:' })
  notePrefix!: string;

  @Prop({ enum: ['whole', 'decimal'], default: 'whole' })
  priceStyle!: BarcodePriceStyle;

  @Prop({ enum: ['sku_spaced', 'raw'], default: 'sku_spaced' })
  barcodeHumanText!: BarcodeHumanTextStyle;

  @Prop({ enum: ['left', 'center', 'right'], default: 'center' })
  alignment!: BarcodeTextAlignment;
}

@Schema({ _id: false })
export class BarcodeLabelBarcodeSettingsSchema implements BarcodeLabelBarcodeSettings {
  @Prop({ required: true, min: 1 })
  heightMm!: number;

  @Prop({ required: true, min: 1 })
  widthMm!: number;
}

@Schema({ _id: false })
export class BarcodeLabelPrintOffsetSchema implements BarcodeLabelPrintOffsetMm {
  @Prop({ default: 0 })
  vertical!: number;

  @Prop({ default: 0 })
  horizontal!: number;
}

@Schema({ timestamps: true, collection: 'barcode_label_designs' })
export class BarcodeLabelDesign {
  @ApiProperty()
  @Prop({ required: true, trim: true })
  name!: string;

  @ApiProperty()
  @Prop({ default: false, index: true })
  isActive!: boolean;

  @ApiProperty({ enum: ['brand_price', 'retail_stacked'] })
  @Prop({ required: true, enum: ['brand_price', 'retail_stacked'] })
  layoutStyle!: BarcodeLayoutStyle;

  @ApiProperty()
  @Prop({ required: true, trim: true })
  printerProfileId!: string;

  @ApiProperty()
  @Prop({ required: true, min: 1 })
  labelWidthMm!: number;

  @ApiProperty()
  @Prop({ required: true, min: 1 })
  labelHeightMm!: number;

  @ApiProperty()
  @Prop({ required: true, min: 1, default: 1 })
  labelsPerRow!: number;

  @ApiProperty()
  @Prop({ required: true, min: 1, default: 203 })
  dpi!: number;

  @Prop({ type: BarcodeLabelFieldsSchema, required: true })
  fields!: BarcodeLabelFields;

  @Prop({ type: BarcodeLabelTextSettingsSchema, required: true })
  text!: BarcodeLabelTextSettings;

  @Prop({ type: BarcodeLabelBarcodeSettingsSchema, required: true })
  barcode!: BarcodeLabelBarcodeSettings;

  @Prop({ type: Object, required: true })
  styles!: Record<string, { sizePt: number; weight: BarcodeFontWeight }>;

  @ApiProperty({ enum: ['none', 'square_border', 'rounded_border', 'price_underline'] })
  @Prop({
    enum: ['none', 'square_border', 'rounded_border', 'price_underline'],
    default: 'none',
  })
  decoration!: BarcodeDecoration;

  @Prop({ type: BarcodeLabelPrintOffsetSchema, required: true })
  printOffsetMm!: BarcodeLabelPrintOffsetMm;

  @ApiProperty({ required: false })
  @Prop({ trim: true })
  customBrandText?: string;
}

export const BarcodeLabelDesignSchema = SchemaFactory.createForClass(BarcodeLabelDesign);
