import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsInt, IsOptional, IsString, Max, Min } from 'class-validator';

export class MyWarehouseQueryDto {
  @ApiProperty({
    required: true,
    description: 'Warehouse location code',
    example: 'loc-001',
  })
  @IsString()
  locationCode!: string;

  @ApiProperty({ required: false, default: 10, minimum: 1, maximum: 50 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(50)
  @IsOptional()
  goodsReceiptLimit?: number;

  @ApiProperty({ required: false, default: 10, minimum: 1, maximum: 50 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(50)
  @IsOptional()
  purchaseOrderLimit?: number;

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
