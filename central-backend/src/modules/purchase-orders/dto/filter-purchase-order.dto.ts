import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsIn, IsInt, IsOptional, IsString, Max, Min } from 'class-validator';

const statuses = ['open', 'awaiting_approval', 'approved', 'partially_received', 'received', 'closed'] as const;

export class FilterPurchaseOrderDto {
  @ApiProperty({ required: false, description: 'Search across poNo, supplier name' })
  @IsString()
  @IsOptional()
  search?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  poNo?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  supplierId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  branchId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  mainDivisionId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  mainLocationId?: string;

  @ApiProperty({ required: false, enum: statuses })
  @IsIn(statuses)
  @IsOptional()
  status?: (typeof statuses)[number];

  // ── Date Range Filters ──

  @ApiProperty({ required: false, description: 'PO date from (inclusive), e.g. 2026-01-01' })
  @IsString()
  @IsOptional()
  poDateFrom?: string;

  @ApiProperty({ required: false, description: 'PO date to (inclusive), e.g. 2026-12-31' })
  @IsString()
  @IsOptional()
  poDateTo?: string;

  @ApiProperty({ required: false, description: 'Delivery date from (inclusive)' })
  @IsString()
  @IsOptional()
  deliveryDateFrom?: string;

  @ApiProperty({ required: false, description: 'Delivery date to (inclusive)' })
  @IsString()
  @IsOptional()
  deliveryDateTo?: string;

  // ── Amount Range Filters ──

  @ApiProperty({ required: false, description: 'Minimum net amount' })
  @Type(() => Number)
  @IsOptional()
  netAmountMin?: number;

  @ApiProperty({ required: false, description: 'Maximum net amount' })
  @Type(() => Number)
  @IsOptional()
  netAmountMax?: number;

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
