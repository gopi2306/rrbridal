import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsInt, IsOptional, IsString, Max, Min } from 'class-validator';

export class StockTallyQueryDto {
  @ApiProperty({ required: true, example: 'store-001' })
  @IsString()
  storeCode!: string;

  @ApiProperty({ required: false, description: 'Filter scanned lines by SKU, barcode, or product name' })
  @IsString()
  @IsOptional()
  search?: string;

  @ApiProperty({ required: false, default: 1, minimum: 1 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @IsOptional()
  page?: number;

  @ApiProperty({ required: false, default: 50, minimum: 1, maximum: 200 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(200)
  @IsOptional()
  limit?: number;
}
