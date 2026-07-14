import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsIn, IsInt, IsOptional, IsString, Max, Min } from 'class-validator';

const PERIODS = ['today', 'week', 'month', 'year', 'custom'] as const;

export class StoreBillMarginQueryDto {
  @ApiProperty({ required: false, description: 'Store code; defaults to first active store' })
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

  @ApiProperty({ required: false, description: 'Filter by salesmanId on invoice payload' })
  @IsString()
  @IsOptional()
  salesmanId?: string;

  @ApiProperty({ required: false, description: 'Filter by POS counter' })
  @IsString()
  @IsOptional()
  posCounter?: string;

  @ApiProperty({ required: false, default: 5000, description: 'Max bill rows (1–5000)' })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(5000)
  @IsOptional()
  limit?: number;
}
