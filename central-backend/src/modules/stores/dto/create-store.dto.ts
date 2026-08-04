import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsBoolean, IsNotEmpty, IsOptional, IsString, ValidateNested } from 'class-validator';
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

  @ApiProperty({
    required: false,
    default: false,
    description: 'Store-wide Central Online mode for all POS counters',
  })
  @IsBoolean()
  @IsOptional()
  preferCentralOnline?: boolean;

  @ApiProperty({ required: false, type: ReceiptPrintSettingsDto })
  @ValidateNested()
  @Type(() => ReceiptPrintSettingsDto)
  @IsOptional()
  receiptPrintSettings?: ReceiptPrintSettingsDto;
}
