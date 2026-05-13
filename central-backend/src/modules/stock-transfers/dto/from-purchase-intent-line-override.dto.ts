import { ApiProperty } from '@nestjs/swagger';
import { IsNotEmpty, IsNumber, IsOptional, IsString, Min } from 'class-validator';

/** Optional per-SKU qty override when creating a transfer from an intent. */
export class FromPurchaseIntentLineOverrideDto {
  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  sku!: string;

  @ApiProperty({ required: false, description: 'If omitted, uses intent requestedQty for this SKU' })
  @IsNumber()
  @Min(0.0001)
  @IsOptional()
  qty?: number;
}
