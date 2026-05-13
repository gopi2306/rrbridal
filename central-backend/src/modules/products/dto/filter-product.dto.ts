import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import {
  IsBoolean,
  IsInt,
  IsOptional,
  IsString,
  Max,
  Min,
} from 'class-validator';

export class FilterProductDto {
  // ── Text Search ──

  @ApiProperty({ required: false, description: 'Search across itemName, shortName, alias, sku, upcEanCode' })
  @IsString()
  @IsOptional()
  search?: string;

  // ── Item Information ──

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  sku?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  upcEanCode?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  manufacturerNameId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  supplierNameId?: string;

  // ── Category Filters ──

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  departmentId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  categoryId?: string;

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

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  colourId?: string;

  // ── Tax Filters ──

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  hsnCodeId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  gstUomId?: string;

  // ── Packing Filters ──

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

  // ── Pricing Filters ──

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  poQtyPolicyId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  sellById?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  batchSelectionId?: string;

  // ── Price Range Filters ──

  @ApiProperty({ required: false, description: 'Minimum MRP' })
  @Type(() => Number)
  @IsOptional()
  mrpMin?: number;

  @ApiProperty({ required: false, description: 'Maximum MRP' })
  @Type(() => Number)
  @IsOptional()
  mrpMax?: number;

  @ApiProperty({ required: false, description: 'Minimum selling price' })
  @Type(() => Number)
  @IsOptional()
  sellingPriceMin?: number;

  @ApiProperty({ required: false, description: 'Maximum selling price' })
  @Type(() => Number)
  @IsOptional()
  sellingPriceMax?: number;

  // ── Reorder Filters ──

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

  // ── Status ──

  @ApiProperty({ required: false })
  @IsBoolean()
  @IsOptional()
  @Type(() => Boolean)
  isActive?: boolean;

  // ── Pagination ──

  @ApiProperty({ required: false, default: 1, minimum: 1 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @IsOptional()
  page?: number;

  @ApiProperty({ required: false, default: 20, minimum: 1, maximum: 500 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(500)
  @IsOptional()
  limit?: number;

  // ── Sorting ──

  @ApiProperty({ required: false, default: 'updatedAt', description: 'Field to sort by' })
  @IsString()
  @IsOptional()
  sortBy?: string;

  @ApiProperty({ required: false, default: 'desc', enum: ['asc', 'desc'] })
  @IsString()
  @IsOptional()
  sortOrder?: 'asc' | 'desc';
}
