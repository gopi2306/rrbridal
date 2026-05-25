import { ApiProperty } from '@nestjs/swagger';
import { IsNotEmpty, IsNumber, IsOptional, IsString, Matches, MaxLength, Min } from 'class-validator';

export class CreatePurchaseIntentLineDto {
  @ApiProperty({
    required: false,
    description: 'Product Mongo ObjectId; if omitted, resolved from sku when a product exists',
  })
  @IsString()
  @IsOptional()
  @Matches(/^[a-fA-F0-9]{24}$/i, { message: 'productId must be a 24-character hex Mongo id' })
  productId?: string;

  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  sku!: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  barcode?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  description?: string;

  @ApiProperty()
  @IsNumber()
  @Min(0.0001)
  requestedQty!: number;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  note?: string;

  @ApiProperty({ required: false, maxLength: 80 })
  @IsString()
  @IsOptional()
  @MaxLength(80)
  stockClassification?: string;

  @ApiProperty({ required: false, maxLength: 40, description: 'Destination kind hint (e.g. warehouse, store)' })
  @IsString()
  @IsOptional()
  @MaxLength(40)
  toKind?: string;

  @ApiProperty({ required: false, description: 'Target Location Mongo ObjectId' })
  @IsString()
  @IsOptional()
  @Matches(/^[a-fA-F0-9]{24}$/i, { message: 'toLocationId must be a 24-character hex Mongo id' })
  toLocationId?: string;

  @ApiProperty({ required: false, maxLength: 500 })
  @IsString()
  @IsOptional()
  @MaxLength(500)
  remarks?: string;
}
