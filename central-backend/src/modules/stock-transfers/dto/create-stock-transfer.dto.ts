import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import {
  IsArray,
  IsIn,
  IsNotEmpty,
  IsOptional,
  IsString,
  Matches,
  MaxLength,
  ValidateIf,
  ValidateNested,
} from 'class-validator';
import { StockTransferLineDto } from './stock-transfer-line.dto';

const directions = ['warehouse_to_store', 'store_to_warehouse'] as const;

export class CreateStockTransferDto {
  @ApiProperty({
    enum: directions,
    required: false,
    default: 'warehouse_to_store',
    description: 'warehouse_to_store (in) or store_to_warehouse (out)',
  })
  @IsOptional()
  @IsIn(directions)
  direction?: (typeof directions)[number];

  @ApiProperty({
    required: false,
    description: 'Mongo ObjectId of source warehouse Location (transfer in)',
  })
  @IsString()
  @IsOptional()
  @Matches(/^[a-fA-F0-9]{24}$/i, { message: 'locationId must be a 24-character hex Mongo id' })
  locationId?: string;

  @ApiProperty({
    required: false,
    description: 'Destination store id (required when direction is warehouse_to_store)',
  })
  @ValidateIf((o: CreateStockTransferDto) => (o.direction ?? 'warehouse_to_store') === 'warehouse_to_store')
  @IsString()
  @IsNotEmpty()
  toStoreId?: string;

  @ApiProperty({
    required: false,
    description: 'Source store id (required when direction is store_to_warehouse)',
  })
  @ValidateIf((o: CreateStockTransferDto) => o.direction === 'store_to_warehouse')
  @IsString()
  @IsNotEmpty()
  fromStoreId?: string;

  @ApiProperty({
    required: false,
    description: 'Mongo ObjectId of destination warehouse Location (transfer out)',
  })
  @IsString()
  @IsOptional()
  @Matches(/^[a-fA-F0-9]{24}$/i, { message: 'toLocationId must be a 24-character hex Mongo id' })
  toLocationId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  transferDate?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  remarks?: string;

  @ApiProperty({ required: false, example: 'Normal Stock', description: 'Defaults to Normal Stock when omitted' })
  @IsString()
  @IsOptional()
  @MaxLength(80)
  stockClassification?: string;

  @ApiProperty({ type: [StockTransferLineDto] })
  @IsArray()
  @ValidateNested({ each: true })
  @Type(() => StockTransferLineDto)
  lines!: StockTransferLineDto[];
}
