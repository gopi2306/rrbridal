import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsArray, IsNotEmpty, IsOptional, IsString, Matches, MaxLength, ValidateNested } from 'class-validator';
import { StockTransferLineDto } from './stock-transfer-line.dto';

export class CreateStockTransferDto {
  @ApiProperty({
    required: false,
    description: 'Mongo ObjectId of an active Location with type warehouse (source site for dispatch)',
  })
  @IsString()
  @IsOptional()
  @Matches(/^[a-fA-F0-9]{24}$/i, { message: 'locationId must be a 24-character hex Mongo id' })
  locationId?: string;

  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  toStoreId!: string;

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
