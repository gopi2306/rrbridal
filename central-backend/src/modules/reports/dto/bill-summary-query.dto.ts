import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsInt, IsOptional, IsString, Matches, Min } from 'class-validator';

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

export class BillSummaryQueryDto {
  @ApiProperty({ required: false, description: 'Store code; defaults to first active store' })
  @IsString()
  @IsOptional()
  storeCode?: string;

  @ApiProperty({ description: 'From date inclusive (YYYY-MM-DD)', example: '2026-06-01' })
  @IsString()
  @Matches(ISO_DATE, { message: 'from must be YYYY-MM-DD' })
  from!: string;

  @ApiProperty({ description: 'To date inclusive (YYYY-MM-DD)', example: '2026-06-30' })
  @IsString()
  @Matches(ISO_DATE, { message: 'to must be YYYY-MM-DD' })
  to!: string;

  @ApiProperty({ required: false, description: 'Optional POS counter filter' })
  @IsString()
  @IsOptional()
  posCounter?: string;

  @ApiProperty({ required: false, default: 10000, minimum: 1, description: 'Max rows returned/exported' })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @IsOptional()
  limit?: number;
}

