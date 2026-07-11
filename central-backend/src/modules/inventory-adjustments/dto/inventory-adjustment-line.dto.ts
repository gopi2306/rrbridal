import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsNotEmpty, IsNumber, IsOptional, IsString, MaxLength, ValidateIf } from 'class-validator';

export class InventoryAdjustmentLineDto {
  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  sku!: string;

  @ApiProperty({ required: false, description: 'Signed quantity change (+ increase, − decrease)' })
  @ValidateIf((dto: InventoryAdjustmentLineDto) => dto.newQty === undefined)
  @Type(() => Number)
  @IsNumber()
  qtyDelta?: number;

  @ApiProperty({ required: false, description: 'Target on-hand quantity; delta is computed from current ledger qty' })
  @ValidateIf((dto: InventoryAdjustmentLineDto) => dto.qtyDelta === undefined)
  @Type(() => Number)
  @IsNumber()
  newQty?: number;

  @ApiProperty({ required: false, maxLength: 500 })
  @IsString()
  @IsOptional()
  @MaxLength(500)
  note?: string;
}
