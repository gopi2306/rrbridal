import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsArray, IsOptional, IsString, Matches, MaxLength, ValidateNested } from 'class-validator';
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

  @ApiProperty({ required: false, example: 'Normal Stock' })
  @IsString()
  @IsOptional()
  @MaxLength(80)
  stockClassification?: string;

  @ApiProperty({
    required: false,
    description: 'Mongo ObjectId of an active warehouse Location (source site)',
  })
  @IsString()
  @IsOptional()
  @Matches(/^[a-fA-F0-9]{24}$/i, { message: 'locationId must be a 24-character hex Mongo id' })
  locationId?: string;
}
