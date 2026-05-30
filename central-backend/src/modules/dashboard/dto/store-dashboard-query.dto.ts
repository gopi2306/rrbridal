import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsInt, IsOptional, IsString, Max, Min } from 'class-validator';

export class StoreDashboardQueryDto {
  @ApiProperty({
    required: false,
    description: 'Store code; defaults to first active store',
    example: 'store-001',
  })
  @IsString()
  @IsOptional()
  storeId?: string;

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

  @ApiProperty({ required: false, default: 10, minimum: 1, maximum: 20 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(20)
  @IsOptional()
  transferLimit?: number;
}
