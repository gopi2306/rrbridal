import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsInt, IsOptional, IsString, Matches, Max, Min } from 'class-validator';

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

export class ItemDetailsReportQueryDto {
  @ApiProperty({
    required: false,
    description: 'From date inclusive (YYYY-MM-DD). Omit for all history from start.',
    example: '2026-01-01',
  })
  @IsString()
  @IsOptional()
  @Matches(ISO_DATE, { message: 'from must be YYYY-MM-DD' })
  from?: string;

  @ApiProperty({
    required: false,
    description: 'To date inclusive (YYYY-MM-DD). Omit for up to today.',
    example: '2026-06-30',
  })
  @IsString()
  @IsOptional()
  @Matches(ISO_DATE, { message: 'to must be YYYY-MM-DD' })
  to?: string;

  @ApiProperty({ required: false, description: 'Exact SKU filter' })
  @IsString()
  @IsOptional()
  sku?: string;

  @ApiProperty({ required: false, description: 'SKU or product name contains' })
  @IsString()
  @IsOptional()
  search?: string;

  @ApiProperty({ required: false, description: 'Narrow sales to a store; SOH shows that store qty' })
  @IsString()
  @IsOptional()
  storeId?: string;

  @ApiProperty({ required: false, description: 'Brand code or Mongo id' })
  @IsString()
  @IsOptional()
  brandId?: string;

  @ApiProperty({ required: false, description: 'Supplier id or code' })
  @IsString()
  @IsOptional()
  supplierId?: string;

  @ApiProperty({ required: false, default: 1000, minimum: 1, maximum: 10000 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(10000)
  @IsOptional()
  limit?: number;

  @ApiProperty({ required: false, default: 0, minimum: 0 })
  @Type(() => Number)
  @IsInt()
  @Min(0)
  @IsOptional()
  offset?: number;
}
