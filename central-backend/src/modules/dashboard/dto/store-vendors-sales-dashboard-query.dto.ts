import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsIn, IsInt, IsOptional, IsString, Max, Min } from 'class-validator';

const PERIODS = ['today', 'week', 'month', 'year', 'custom'] as const;

/** All-vendors vendorwise sales tab (no supplierId). */
export class StoreVendorsSalesDashboardQueryDto {
  @ApiProperty({ required: false, description: 'Store code or Mongo _id; defaults to first active store' })
  @IsString()
  @IsOptional()
  storeId?: string;

  @ApiProperty({ required: false, default: 'today', enum: PERIODS })
  @IsIn(PERIODS)
  @IsOptional()
  period?: (typeof PERIODS)[number];

  @ApiProperty({ required: false, description: 'YYYY-MM-DD; required when period=custom' })
  @IsString()
  @IsOptional()
  from?: string;

  @ApiProperty({ required: false, description: 'YYYY-MM-DD; required when period=custom' })
  @IsString()
  @IsOptional()
  to?: string;

  @ApiProperty({ required: false, description: 'Calendar year for period=month or year' })
  @Type(() => Number)
  @IsInt()
  @Min(2000)
  @Max(2100)
  @IsOptional()
  year?: number;

  @ApiProperty({ required: false, description: 'Calendar month 1-12 for period=month' })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(12)
  @IsOptional()
  month?: number;

  @ApiProperty({ required: false, default: 50, minimum: 1, maximum: 200 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(200)
  @IsOptional()
  invoiceLimit?: number;

  @ApiProperty({ required: false, default: 20, minimum: 1, maximum: 100 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(100)
  @IsOptional()
  returnDetailLimit?: number;
}
