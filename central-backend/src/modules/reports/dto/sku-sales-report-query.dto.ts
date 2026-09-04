import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsInt, IsNumber, IsOptional, IsString, Matches, Max, Min } from 'class-validator';
import { TABULAR_EXPORT_MAX_ROWS } from '../../../common/tabular-export';

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

export class SkuSalesReportQueryDto {
  @ApiProperty({ required: false, description: 'Store code; defaults to first active store' })
  @IsString()
  @IsOptional()
  storeCode?: string;

  @ApiProperty({ description: 'From business date inclusive (YYYY-MM-DD)' })
  @IsString()
  @Matches(ISO_DATE, { message: 'from must be YYYY-MM-DD' })
  from!: string;

  @ApiProperty({ description: 'To business date inclusive (YYYY-MM-DD)' })
  @IsString()
  @Matches(ISO_DATE, { message: 'to must be YYYY-MM-DD' })
  to!: string;

  @ApiProperty({ required: false, description: 'Supplier name, SKU, or product name contains' })
  @IsString()
  @IsOptional()
  search?: string;

  @ApiProperty({ required: false, description: 'Exact POS counter' })
  @IsString()
  @IsOptional()
  posCounter?: string;

  @ApiProperty({
    required: false,
    default: TABULAR_EXPORT_MAX_ROWS,
    minimum: 1,
    maximum: TABULAR_EXPORT_MAX_ROWS,
    description: 'Invoice safety cap, or top SKU count for fast sellers',
  })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(TABULAR_EXPORT_MAX_ROWS)
  @IsOptional()
  limit?: number;

  @ApiProperty({
    required: false,
    description:
      'Store-wide match qty for Fast Sellers low-stock flag (availableQty ≤ matchQty). 0 disables flagging.',
    minimum: 0,
  })
  @Type(() => Number)
  @IsNumber()
  @Min(0)
  @IsOptional()
  matchQty?: number;
}
