import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsDateString, IsInt, IsNumber, IsOptional, IsString, Max, Min } from 'class-validator';

export class InventoryFilteredQueryDto {
  @ApiProperty({
    required: false,
    description: 'Store code. When provided, returns only that store inventory; otherwise returns warehouse inventory.',
    example: 'store-001',
  })
  @IsString()
  @IsOptional()
  storeCode?: string;

  @ApiProperty({ required: false, description: 'Filter by SKU, barcode, or product name' })
  @IsString()
  @IsOptional()
  search?: string;

  @ApiProperty({ required: false, description: 'Filter by department ObjectId' })
  @IsString()
  @IsOptional()
  departmentId?: string;

  @ApiProperty({ required: false, description: 'Filter by category ObjectId' })
  @IsString()
  @IsOptional()
  categoryId?: string;

  @ApiProperty({ required: false, description: 'Filter by sub category ObjectId' })
  @IsString()
  @IsOptional()
  subCategoryId?: string;

  @ApiProperty({ required: false, description: 'Filter by supplier ObjectId' })
  @IsString()
  @IsOptional()
  supplierId?: string;

  @ApiProperty({ required: false, description: 'Alias for supplierId / supplierNameId product ref' })
  @IsString()
  @IsOptional()
  supplierNameId?: string;

  @ApiProperty({ required: false, minimum: 0, description: 'Inclusive minimum stock quantity' })
  @Type(() => Number)
  @IsNumber()
  @Min(0)
  @IsOptional()
  minQty?: number;

  @ApiProperty({ required: false, minimum: 0, description: 'Inclusive maximum stock quantity' })
  @Type(() => Number)
  @IsNumber()
  @Min(0)
  @IsOptional()
  maxQty?: number;

  @ApiProperty({ required: false, minimum: 0, description: 'Inclusive minimum stock age in days' })
  @Type(() => Number)
  @IsInt()
  @Min(0)
  @IsOptional()
  minAgeDays?: number;

  @ApiProperty({ required: false, minimum: 0, description: 'Inclusive maximum stock age in days' })
  @Type(() => Number)
  @IsInt()
  @Min(0)
  @IsOptional()
  maxAgeDays?: number;

  @ApiProperty({
    required: false,
    description: 'Inclusive minimum inward date (YYYY-MM-DD) for stock age base date',
    example: '2026-01-01',
  })
  @IsDateString()
  @IsOptional()
  fromDate?: string;

  @ApiProperty({
    required: false,
    description: 'Inclusive maximum inward date (YYYY-MM-DD) for stock age base date',
    example: '2026-07-31',
  })
  @IsDateString()
  @IsOptional()
  toDate?: string;

  @ApiProperty({ required: false, default: 1, minimum: 1 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @IsOptional()
  page?: number;

  @ApiProperty({ required: false, default: 200, minimum: 1, maximum: 500 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(500)
  @IsOptional()
  limit?: number;
}
