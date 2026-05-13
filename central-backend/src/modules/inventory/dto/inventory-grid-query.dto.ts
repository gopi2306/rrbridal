import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsInt, IsOptional, IsString, Max, Min } from 'class-validator';

export class InventoryGridQueryDto {
  @ApiProperty({ required: false, description: 'Filter by SKU, barcode, or product name' })
  @IsString()
  @IsOptional()
  search?: string;

  @ApiProperty({
    required: false,
    description: 'When set, storeQty is quantity at this store only; when omitted, storeQty sums all store locations',
  })
  @IsString()
  @IsOptional()
  storeId?: string;

  @ApiProperty({ required: false, default: 200 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(500)
  @IsOptional()
  limit?: number;
}
