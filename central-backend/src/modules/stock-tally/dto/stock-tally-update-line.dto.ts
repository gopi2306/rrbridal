import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsInt, IsString, Min } from 'class-validator';

export class StockTallyUpdateLineDto {
  @ApiProperty({ example: 'store-001' })
  @IsString()
  storeCode!: string;

  @ApiProperty({ example: 'SKU-000235' })
  @IsString()
  sku!: string;

  @ApiProperty({ minimum: 0 })
  @Type(() => Number)
  @IsInt()
  @Min(0)
  scannedQty!: number;
}
