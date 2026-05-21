import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsIn, IsInt, IsOptional, IsString, Max, Min } from 'class-validator';

const statuses = ['draft', 'in_transit', 'awaiting_intake', 'completed', 'cancelled'] as const;

export class FilterStockTransferDto {
  @ApiProperty({ required: false, description: 'Search across transferNo, remarks, line sku/description' })
  @IsString()
  @IsOptional()
  search?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  transferNo?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  toStoreId?: string;

  @ApiProperty({ required: false, description: 'Source warehouse location Mongo ObjectId' })
  @IsString()
  @IsOptional()
  fromLocationId?: string;

  @ApiProperty({ required: false, description: 'Linked purchase intent Mongo ObjectId' })
  @IsString()
  @IsOptional()
  purchaseIntentId?: string;

  @ApiProperty({ required: false, enum: statuses })
  @IsIn(statuses)
  @IsOptional()
  status?: (typeof statuses)[number];

  @ApiProperty({ required: false, description: 'Stock classification label (e.g. Normal Stock)' })
  @IsString()
  @IsOptional()
  stockClassification?: string;

  @ApiProperty({ required: false, description: 'Filter transfers containing a line with this sku (case-insensitive partial match)' })
  @IsString()
  @IsOptional()
  sku?: string;

  @ApiProperty({ required: false, description: 'Transfer date from (inclusive), e.g. 2026-01-01' })
  @IsString()
  @IsOptional()
  transferDateFrom?: string;

  @ApiProperty({ required: false, description: 'Transfer date to (inclusive), e.g. 2026-12-31' })
  @IsString()
  @IsOptional()
  transferDateTo?: string;

  @ApiProperty({ required: false, description: 'Created at from (inclusive), ISO date e.g. 2026-01-01' })
  @IsString()
  @IsOptional()
  createdAtFrom?: string;

  @ApiProperty({ required: false, description: 'Created at to (inclusive), ISO date e.g. 2026-12-31' })
  @IsString()
  @IsOptional()
  createdAtTo?: string;

  @ApiProperty({ required: false, description: 'Updated at from (inclusive), ISO date' })
  @IsString()
  @IsOptional()
  updatedAtFrom?: string;

  @ApiProperty({ required: false, description: 'Updated at to (inclusive), ISO date' })
  @IsString()
  @IsOptional()
  updatedAtTo?: string;

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

  @ApiProperty({ required: false, default: 'updatedAt', description: 'Field to sort by' })
  @IsString()
  @IsOptional()
  sortBy?: string;

  @ApiProperty({ required: false, default: 'desc', enum: ['asc', 'desc'] })
  @IsString()
  @IsOptional()
  sortOrder?: 'asc' | 'desc';
}
