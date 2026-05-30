import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsInt, IsOptional, IsString, Max, Min } from 'class-validator';

export class WarehouseDashboardQueryDto {
  @ApiProperty({
    required: false,
    description: 'Warehouse location code; defaults to first active warehouse location',
  })
  @IsString()
  @IsOptional()
  locationCode?: string;

  @ApiProperty({ required: false, default: 10, minimum: 1, maximum: 50 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(50)
  @IsOptional()
  lowStockLimit?: number;

  @ApiProperty({ required: false, default: 10, minimum: 1, maximum: 50 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(50)
  @IsOptional()
  activityLimit?: number;

  @ApiProperty({ required: false, default: 7, minimum: 1, maximum: 90 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(90)
  @IsOptional()
  inboundDays?: number;
}
