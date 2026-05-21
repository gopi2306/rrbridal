import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { ArrayMinSize, IsArray, IsNotEmpty, IsOptional, IsString, ValidateNested } from 'class-validator';
import { StockTransferLineDto } from './stock-transfer-line.dto';

export class ReceiveStockTransferDto {
  @ApiProperty({
    description: 'Must match toStoreId (transfer in) or fromStoreId (transfer out)',
  })
  @IsString()
  @IsNotEmpty()
  storeId!: string;

  @ApiProperty({ type: [StockTransferLineDto] })
  @IsArray()
  @ArrayMinSize(1)
  @ValidateNested({ each: true })
  @Type(() => StockTransferLineDto)
  lines!: StockTransferLineDto[];

  @ApiProperty({ required: false, description: 'ISO timestamp; defaults to server time when omitted' })
  @IsString()
  @IsOptional()
  receivedAt?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  receivedBy?: string;
}
