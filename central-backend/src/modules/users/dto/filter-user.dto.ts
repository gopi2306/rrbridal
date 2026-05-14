import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsIn, IsInt, IsOptional, IsString, Max, Min } from 'class-validator';

const roles = ['super_admin', 'admin', 'warehouse', 'store', 'procurement'] as const;
const locations = ['all', 'warehouse', 'store'] as const;
const statuses = ['active', 'invited', 'disabled'] as const;

export class FilterUserDto {
  @ApiProperty({ required: false, description: 'Search across name, email' })
  @IsString()
  @IsOptional()
  search?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  email?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  name?: string;

  @ApiProperty({ required: false, enum: roles })
  @IsIn(roles)
  @IsOptional()
  role?: (typeof roles)[number];

  @ApiProperty({ required: false, enum: locations })
  @IsIn(locations)
  @IsOptional()
  locationKind?: (typeof locations)[number];

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  storeId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  warehouseLocationCode?: string;

  @ApiProperty({ required: false, enum: statuses })
  @IsIn(statuses)
  @IsOptional()
  status?: (typeof statuses)[number];

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

  @ApiProperty({ required: false, default: 'createdAt', description: 'Field to sort by' })
  @IsString()
  @IsOptional()
  sortBy?: string;

  @ApiProperty({ required: false, default: 'desc', enum: ['asc', 'desc'] })
  @IsString()
  @IsOptional()
  sortOrder?: 'asc' | 'desc';
}
