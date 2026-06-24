import { ApiProperty, ApiPropertyOptional } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsInt, IsOptional, IsString, Min, ValidateIf } from 'class-validator';

export class StockTallyLineInputDto {
  @ApiProperty({ example: 'SKU-000235' })
  @IsString()
  sku!: string;

  @ApiPropertyOptional({ minimum: 0, description: 'Scanned quantity' })
  @ValidateIf((dto: StockTallyLineInputDto) => dto.qty === undefined)
  @Type(() => Number)
  @IsInt()
  @Min(0)
  scannedQty?: number;

  @ApiPropertyOptional({ minimum: 0, description: 'Alias for scannedQty' })
  @ValidateIf((dto: StockTallyLineInputDto) => dto.scannedQty === undefined)
  @Type(() => Number)
  @IsInt()
  @Min(0)
  qty?: number;
}
