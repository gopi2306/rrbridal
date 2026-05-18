import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsNotEmpty, IsOptional, IsString, ValidateNested } from 'class-validator';
import { ReceiptPrintSettingsDto } from './receipt-print-settings.dto';

export class CreateStoreDto {
  @ApiProperty({ example: 'store-001' })
  @IsString()
  @IsNotEmpty()
  code!: string;

  @ApiProperty({ example: 'RR Bridal - Main Branch' })
  @IsString()
  @IsNotEmpty()
  name!: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  address?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  phone?: string;

  @ApiProperty({ required: false, type: ReceiptPrintSettingsDto })
  @ValidateNested()
  @Type(() => ReceiptPrintSettingsDto)
  @IsOptional()
  receiptPrintSettings?: ReceiptPrintSettingsDto;
}
