import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsInt, IsOptional, IsString, Max, Min } from 'class-validator';

export class MyWarehouseInventoryQueryDto {
  @ApiProperty({
    required: true,
    description: 'Warehouse location code',
    example: 'loc-001',
  })
  @IsString()
  locationCode!: string;

  @ApiProperty({ required: false, description: 'Filter by SKU, barcode, or product name' })
  @IsString()
  @IsOptional()
  search?: string;

  @ApiProperty({ required: false, default: 1, minimum: 1 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @IsOptional()
  page?: number;

  @ApiProperty({ required: false, default: 20, minimum: 1, maximum: 100 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(100)
  @IsOptional()
  limit?: number;
}
