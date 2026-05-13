import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsArray, IsOptional, IsString, ValidateNested } from 'class-validator';
import { StockTransferLineDto } from './stock-transfer-line.dto';

export class UpdateStockTransferDto {
  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  transferDate?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  remarks?: string;

  @ApiProperty({ type: [StockTransferLineDto], required: false })
  @IsArray()
  @ValidateNested({ each: true })
  @Type(() => StockTransferLineDto)
  @IsOptional()
  lines?: StockTransferLineDto[];
}
