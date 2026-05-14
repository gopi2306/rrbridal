import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsArray, IsOptional, IsString, Matches, MaxLength, ValidateNested } from 'class-validator';
import { StockTransferLineDto } from './stock-transfer-line.dto';

export class UpdateStockTransferDto {
  @ApiProperty({
    required: false,
    description: 'Mongo ObjectId of an active warehouse Location (draft only)',
  })
  @IsString()
  @IsOptional()
  @Matches(/^[a-fA-F0-9]{24}$/i, { message: 'locationId must be a 24-character hex Mongo id' })
  locationId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  transferDate?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  remarks?: string;

  @ApiProperty({ required: false, example: 'Normal Stock' })
  @IsString()
  @IsOptional()
  @MaxLength(80)
  stockClassification?: string;

  @ApiProperty({ type: [StockTransferLineDto], required: false })
  @IsArray()
  @ValidateNested({ each: true })
  @Type(() => StockTransferLineDto)
  @IsOptional()
  lines?: StockTransferLineDto[];
}
