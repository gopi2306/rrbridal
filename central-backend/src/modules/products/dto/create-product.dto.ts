import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import {
  IsArray,
  IsBoolean,
  IsNotEmpty,
  IsNumber,
  IsOptional,
  IsString,
  Max,
  Min,
  ValidateNested,
} from 'class-validator';
import { ProductMediaItemDto } from './product-media-item.dto';

export class CreateProductDto {
  // ── Item Information ──

  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  itemName!: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  shortName?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  alias?: string;

  @ApiProperty({
    required: false,
    description: 'Omit to auto-generate the next SKU (e.g. SKU-000006) on the server',
    example: 'SKU-000123',
  })
  @IsString()
  @IsOptional()
  sku?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  manufacturerNameId?: string;

  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  supplierNameId!: string;

  // ── Category Information ──

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  itemProductType?: string;

  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  departmentId!: string;

  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  categoryId!: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  subCategoryId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  brandId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  weightAndSizeId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  weightPerGmOrMlId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  offerGroupId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  productStatusId?: string;

  @ApiProperty({
    required: false,
    type: [String],
    description: 'Colour master ids (supports 1–3 colours for multi-colour products)',
    example: ['507f1f77bcf86cd799439011', '507f1f77bcf86cd799439012'],
  })
  @IsArray()
  @IsString({ each: true })
  @IsOptional()
  colourIds?: string[];

  @ApiProperty({ required: false, description: 'Colour type master id (1/2/3 Color)' })
  @IsString()
  @IsOptional()
  colourTypeId?: string;

  // ── Tax Information ──

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  hsnCodeId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  gstCode?: string;

  @ApiProperty({ required: false, description: 'GST percentage (0-100)' })
  @IsNumber()
  @Min(0)
  @Max(100)
  @IsOptional()
  gstPercent?: number;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  gstUomId?: string;

  // ── EAN Code / Barcode ──

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  upcEanCode?: string;

  // ── Packing ──

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  subUomConversion?: number;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  uomSubId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  batchExpiryDetailId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  itemPrepStatusId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  packedConfirmationId?: string;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  grindingCharge?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  weightGms?: number;

  // ── Item Properties ──

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  decimalPoint?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  minimumShelfFit?: number;

  // ── Pricing ──

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  poQtyPolicyId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  sellById?: string;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  itemPerUnit?: number;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  batchSelectionId?: string;

  @ApiProperty({ required: false })
  @IsBoolean()
  @IsOptional()
  itemDiscountAllowed?: boolean;

  @ApiProperty({ required: false })
  @IsBoolean()
  @IsOptional()
  isWeighable?: boolean;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  unit?: string;

  @ApiProperty({ required: false })
  @IsNumber()
  @Min(0)
  @IsOptional()
  costPrice?: number;

  @ApiProperty({
    required: false,
    description: 'Minimum gross margin % on cost (e.g. 20 → min rate = cost × 1.20). 0 = no floor.',
  })
  @IsNumber()
  @Min(0)
  @Max(1000)
  @IsOptional()
  marginPercent?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  mrp?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  sellingPrice?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  storePrice?: number;

  // ── Reorder Configurations ──

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  skuTypeId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  skuOrderGroupId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  indentTypeId?: string;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  minStock?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  reorderLevel?: number;

  // ── Media ──

  @ApiProperty({
    required: false,
    type: [ProductMediaItemDto],
    description: 'Product images/files with optional per-image descriptions',
    example: [
      {
        url: '/api/media/files/products/507f1f77bcf86cd799439011.jpg',
        description: 'Front view',
        colourIds: ['507f1f77bcf86cd799439012'],
      },
    ],
  })
  @IsArray()
  @ValidateNested({ each: true })
  @Type(() => ProductMediaItemDto)
  @IsOptional()
  mediaItems?: ProductMediaItemDto[];

  /** @deprecated Prefer mediaItems. Converted to mediaItems on write. */
  @ApiProperty({
    required: false,
    type: [String],
    deprecated: true,
    description: 'Deprecated — use mediaItems. Legacy public URLs only.',
    example: ['/api/media/files/products/507f1f77bcf86cd799439011.jpg'],
  })
  @IsArray()
  @IsString({ each: true })
  @IsOptional()
  mediaUrls?: string[];

  // ── Status ──

  @ApiProperty({ required: false, default: true })
  @IsBoolean()
  @IsOptional()
  isActive?: boolean;
}
