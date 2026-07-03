import { ApiProperty } from '@nestjs/swagger';
import { IsOptional, IsString, Matches } from 'class-validator';

const YMD_PATTERN = /^\d{4}-\d{2}-\d{2}$/;

export class StoreDayCloseDashboardQueryDto {
  @ApiProperty({
    required: false,
    example: 'store-001',
    description: 'Store code; defaults to first active store when omitted',
  })
  @IsOptional()
  @IsString()
  storeId?: string;

  @ApiProperty({
    required: false,
    example: '2026-06-03',
    description: 'Business calendar day (YYYY-MM-DD); defaults to today',
  })
  @IsOptional()
  @IsString()
  @Matches(YMD_PATTERN, { message: 'date must be YYYY-MM-DD' })
  date?: string;

  @ApiProperty({
    required: false,
    example: '2026-06-03',
    description: 'Alias of `date` (YYYY-MM-DD)',
  })
  @IsOptional()
  @IsString()
  @Matches(YMD_PATTERN, { message: 'businessDate must be YYYY-MM-DD' })
  businessDate?: string;

  @ApiProperty({
    required: false,
    example: '1',
    description: 'POS counter number; omit for all counters',
  })
  @IsOptional()
  @IsString()
  posCounter?: string;
}
