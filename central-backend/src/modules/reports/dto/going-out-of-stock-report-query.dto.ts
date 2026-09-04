import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsIn, IsInt, IsNumber, IsOptional, IsString, Max, Min } from 'class-validator';
import { TABULAR_EXPORT_MAX_ROWS } from '../../../common/tabular-export';

export class GoingOutOfStockReportQueryDto {
  @ApiProperty({ required: false, description: 'Store code; defaults to first active store' })
  @IsString()
  @IsOptional()
  storeCode?: string;

  @ApiProperty({ required: false, description: 'SKU, product name, or supplier contains' })
  @IsString()
  @IsOptional()
  search?: string;

  @ApiProperty({ required: false, enum: ['low', 'critical'], description: 'Filter by status' })
  @IsString()
  @IsIn(['low', 'critical'])
  @IsOptional()
  status?: 'low' | 'critical';

  @ApiProperty({
    required: false,
    description:
      'Store-wide match qty when product MOQ/min/reorder is unset or ≤ 0. When > 0, all active products are considered.',
    minimum: 0,
  })
  @Type(() => Number)
  @IsNumber()
  @Min(0)
  @IsOptional()
  matchQty?: number;

  @ApiProperty({
    required: false,
    default: TABULAR_EXPORT_MAX_ROWS,
    minimum: 1,
    maximum: TABULAR_EXPORT_MAX_ROWS,
  })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(TABULAR_EXPORT_MAX_ROWS)
  @IsOptional()
  limit?: number;
}
