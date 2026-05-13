import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsArray, IsOptional, ValidateNested } from 'class-validator';
import { FromPurchaseIntentLineOverrideDto } from './from-purchase-intent-line-override.dto';

export class CreateFromPurchaseIntentDto {
  @ApiProperty({
    required: false,
    type: [FromPurchaseIntentLineOverrideDto],
    description: 'Per-SKU qty overrides; SKUs not listed use intent requestedQty',
  })
  @IsArray()
  @ValidateNested({ each: true })
  @Type(() => FromPurchaseIntentLineOverrideDto)
  @IsOptional()
  lineOverrides?: FromPurchaseIntentLineOverrideDto[];
}
