import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsInt, IsOptional, IsString, Min } from 'class-validator';

export class StockTallyScanDto {
  @ApiProperty({ example: 'store-001' })
  @IsString()
  storeCode!: string;

  @ApiProperty({ description: 'Barcode (upcEanCode) or SKU', example: '8901001000012' })
  @IsString()
  barcodeOrSku!: string;

  @ApiProperty({ required: false, default: 1, minimum: 1, description: 'Quantity to add per scan' })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @IsOptional()
  qtyDelta?: number;
}
