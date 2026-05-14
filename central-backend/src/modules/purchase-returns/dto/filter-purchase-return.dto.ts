import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsInt, IsOptional, IsString, Max, Min } from 'class-validator';

export class FilterPurchaseReturnDto {
  @ApiProperty({ required: false, description: 'Search across return number, supplier name, PUC out slip number' })
  @IsString()
  @IsOptional()
  search?: string;

  @ApiProperty({ required: false, description: 'Exact purchase return number (case-insensitive), e.g. PR-001' })
  @IsString()
  @IsOptional()
  purchaseReturnNo?: string;

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

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  pucOutSlipNo?: string;

  @ApiProperty({ required: false, description: 'Return date from (inclusive)' })
  @IsString()
  @IsOptional()
  purchaseReturnDateFrom?: string;

  @ApiProperty({ required: false, description: 'Return date to (inclusive)' })
  @IsString()
  @IsOptional()
  purchaseReturnDateTo?: string;

  @ApiProperty({ required: false, description: 'Minimum net amount' })
  @Type(() => Number)
  @IsOptional()
  netAmountMin?: number;

  @ApiProperty({ required: false, description: 'Maximum net amount' })
  @Type(() => Number)
  @IsOptional()
  netAmountMax?: number;

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
