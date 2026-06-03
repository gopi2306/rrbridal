import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsInt, IsOptional, IsString, Max, Min } from 'class-validator';

export class MyStoreQueryDto {
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
  purchaseIndentLimit?: number;

  @ApiProperty({ required: false, default: 10, minimum: 1, maximum: 50 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(50)
  @IsOptional()
  transferInLimit?: number;

  @ApiProperty({ required: false, default: 10, minimum: 1, maximum: 50 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(50)
  @IsOptional()
  transferOutLimit?: number;

  @ApiProperty({ required: false, default: 20, minimum: 1, maximum: 100 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(100)
  @IsOptional()
  inventoryPreviewLimit?: number;
}
