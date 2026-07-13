import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import {
  IsBoolean,
  IsIn,
  IsInt,
  IsNumber,
  IsObject,
  IsOptional,
  IsString,
  Max,
  Min,
  ValidateNested,
} from 'class-validator';
import {
  BARCODE_DECORATIONS,
  BARCODE_FIELD_STYLE_KEYS,
  BARCODE_FONT_WEIGHTS,
  BARCODE_HUMAN_TEXT_STYLES,
  BARCODE_LAYOUT_STYLES,
  BARCODE_PRICE_STYLES,
  BARCODE_PRODUCT_NAME_SOURCES,
  BARCODE_TEXT_ALIGNMENTS,
  type BarcodeFieldStyleKey,
} from '../barcode-label-design.types';

class BarcodeLabelFieldsDto {
  @ApiProperty({ required: false })
  @IsBoolean()
  @IsOptional()
  productName?: boolean;

  @ApiProperty({ required: false })
  @IsBoolean()
  @IsOptional()
  designSku?: boolean;

  @ApiProperty({ required: false })
  @IsBoolean()
  @IsOptional()
  sellingPrice?: boolean;

  @ApiProperty({ required: false })
  @IsBoolean()
  @IsOptional()
  sizeNote?: boolean;

  @ApiProperty({ required: false })
  @IsBoolean()
  @IsOptional()
  batchNumber?: boolean;

  @ApiProperty({ required: false })
  @IsBoolean()
  @IsOptional()
  expiryDate?: boolean;

  @ApiProperty({ required: false })
  @IsBoolean()
  @IsOptional()
  brandName?: boolean;
}

class BarcodeLabelTextSettingsDto {
  @ApiProperty({ required: false, enum: BARCODE_PRODUCT_NAME_SOURCES })
  @IsIn([...BARCODE_PRODUCT_NAME_SOURCES])
  @IsOptional()
  productNameSource?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  designNoPrefix?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  pricePrefix?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  notePrefix?: string;

  @ApiProperty({ required: false, enum: BARCODE_PRICE_STYLES })
  @IsIn([...BARCODE_PRICE_STYLES])
  @IsOptional()
  priceStyle?: string;

  @ApiProperty({ required: false, enum: BARCODE_HUMAN_TEXT_STYLES })
  @IsIn([...BARCODE_HUMAN_TEXT_STYLES])
  @IsOptional()
  barcodeHumanText?: string;

  @ApiProperty({ required: false, enum: BARCODE_TEXT_ALIGNMENTS })
  @IsIn([...BARCODE_TEXT_ALIGNMENTS])
  @IsOptional()
  alignment?: string;
}

class BarcodeLabelBarcodeSettingsDto {
  @ApiProperty({ minimum: 1 })
  @Type(() => Number)
  @IsNumber()
  @Min(1)
  @Max(25)
  heightMm!: number;

  @ApiProperty({ minimum: 1 })
  @Type(() => Number)
  @IsNumber()
  @Min(1)
  @Max(80)
  widthMm!: number;
}

class BarcodeLabelFieldStyleDto {
  @ApiProperty({ minimum: 1 })
  @Type(() => Number)
  @IsNumber()
  @Min(1)
  @Max(24)
  sizePt!: number;

  @ApiProperty({ enum: BARCODE_FONT_WEIGHTS })
  @IsIn([...BARCODE_FONT_WEIGHTS])
  weight!: string;
}

class BarcodeLabelPrintOffsetDto {
  @ApiProperty({ required: false })
  @Type(() => Number)
  @IsNumber()
  @IsOptional()
  vertical?: number;

  @ApiProperty({ required: false })
  @Type(() => Number)
  @IsNumber()
  @IsOptional()
  horizontal?: number;
}

export class CreateBarcodeLabelDesignDto {
  @ApiProperty()
  @IsString()
  name!: string;

  @ApiProperty({ enum: BARCODE_LAYOUT_STYLES })
  @IsIn([...BARCODE_LAYOUT_STYLES])
  layoutStyle!: string;

  @ApiProperty()
  @IsString()
  printerProfileId!: string;

  @ApiProperty({ required: false })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(120)
  @IsOptional()
  labelWidthMm?: number;

  @ApiProperty({ required: false })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(120)
  @IsOptional()
  labelHeightMm?: number;

  @ApiProperty({ required: false })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(4)
  @IsOptional()
  labelsPerRow?: number;

  @ApiProperty({ required: false })
  @Type(() => Number)
  @IsInt()
  @Min(72)
  @Max(600)
  @IsOptional()
  dpi?: number;

  @ApiProperty({ required: false, type: BarcodeLabelFieldsDto })
  @ValidateNested()
  @Type(() => BarcodeLabelFieldsDto)
  @IsOptional()
  fields?: BarcodeLabelFieldsDto;

  @ApiProperty({ required: false, type: BarcodeLabelTextSettingsDto })
  @ValidateNested()
  @Type(() => BarcodeLabelTextSettingsDto)
  @IsOptional()
  text?: BarcodeLabelTextSettingsDto;

  @ApiProperty({ type: BarcodeLabelBarcodeSettingsDto })
  @ValidateNested()
  @Type(() => BarcodeLabelBarcodeSettingsDto)
  barcode!: BarcodeLabelBarcodeSettingsDto;

  @ApiProperty({
    required: false,
    description: 'Field style map keyed by productName, designSku, sellingPrice, etc.',
  })
  @IsObject()
  @IsOptional()
  styles?: Partial<Record<BarcodeFieldStyleKey, BarcodeLabelFieldStyleDto>>;

  @ApiProperty({ required: false, enum: BARCODE_DECORATIONS })
  @IsIn([...BARCODE_DECORATIONS])
  @IsOptional()
  decoration?: string;

  @ApiProperty({ required: false, type: BarcodeLabelPrintOffsetDto })
  @ValidateNested()
  @Type(() => BarcodeLabelPrintOffsetDto)
  @IsOptional()
  printOffsetMm?: BarcodeLabelPrintOffsetDto;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  customBrandText?: string;

  @ApiProperty({ required: false, description: 'Set true to activate immediately after create' })
  @IsBoolean()
  @IsOptional()
  activate?: boolean;
}
