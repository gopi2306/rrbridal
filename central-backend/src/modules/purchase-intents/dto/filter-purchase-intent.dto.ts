import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsIn, IsInt, IsOptional, IsString, Max, Min } from 'class-validator';

const statuses = [
  'submitted',
  'under_review',
  'approved',
  'rejected',
  'cancelled',
  'fulfilled',
] as const;

export class FilterPurchaseIntentDto {
  @ApiProperty({ required: false, description: 'Search across intentNo, remarks, line sku/barcode/description' })
  @IsString()
  @IsOptional()
  search?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  intentNo?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  storeId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  deviceId?: string;

  @ApiProperty({ required: false, description: 'Outbox eventId when created from store sync' })
  @IsString()
  @IsOptional()
  sourceEventId?: string;

  @ApiProperty({ required: false, enum: statuses })
  @IsIn(statuses)
  @IsOptional()
  status?: (typeof statuses)[number];

  @ApiProperty({ required: false, description: 'Filter intents containing a line with this sku (case-insensitive partial match)' })
  @IsString()
  @IsOptional()
  sku?: string;

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
