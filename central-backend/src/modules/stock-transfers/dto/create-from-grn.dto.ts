import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import {
  IsArray,
  IsNotEmpty,
  IsOptional,
  IsString,
  Matches,
  MaxLength,
  ValidateNested,
} from 'class-validator';
import { FromPurchaseIntentLineOverrideDto } from './from-purchase-intent-line-override.dto';

export class CreateFromGrnDto {
  @ApiProperty({ description: 'Destination store id for the transfer' })
  @IsString()
  @IsNotEmpty()
  toStoreId!: string;

  @ApiProperty({
    required: false,
    type: [FromPurchaseIntentLineOverrideDto],
    description: 'Per-SKU qty overrides; SKUs not listed use GRN receivedQty',
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

  @ApiProperty({ required: false, description: 'Operator who confirmed receipt' })
  @IsString()
  @IsOptional()
  @MaxLength(120)
  receivedBy?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  @MaxLength(500)
  remarks?: string;
}
