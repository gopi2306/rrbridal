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

export class FilterSupplierDto {
  // ── Text Search ──

  @ApiProperty({ required: false, description: 'Search across name, mobileNo, emailId, gstNumber, panNumber, contactPerson' })
  @IsString()
  @IsOptional()
  search?: string;

  // ── Identity Filters ──

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  name?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  gstNumber?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  gstStateCode?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  gstRegistrationType?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  panNumber?: string;

  // ── Contact Filters ──

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  mobileNo?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  emailId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  contactPerson?: string;

  // ── Address Filters ──

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  country?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  state?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  city?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  pin?: string;

  // ── Type Filters ──

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  businessRelatedType?: string;

  @ApiProperty({ required: false })
  @IsBoolean()
  @IsOptional()
  @Type(() => Boolean)
  isSupplier?: boolean;

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
