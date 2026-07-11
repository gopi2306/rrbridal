import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsIn, IsInt, IsOptional, IsString, Max, Min } from 'class-validator';

export class FilterInventoryAdjustmentQueryDto {
  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  storeCode?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  locationCode?: string;

  @ApiProperty({ required: false, enum: ['store', 'warehouse'] })
  @IsIn(['store', 'warehouse'])
  @IsOptional()
  locationKind?: 'store' | 'warehouse';

  @ApiProperty({ required: false, description: 'Search adjustment no, reason, or SKU in lines' })
  @IsString()
  @IsOptional()
  search?: string;

  @ApiProperty({ required: false, default: 1 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @IsOptional()
  page?: number;

  @ApiProperty({ required: false, default: 20 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(100)
  @IsOptional()
  limit?: number;
}
