import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsBoolean, IsIn, IsObject, IsOptional, IsString, ValidateNested } from 'class-validator';
import { ReceiptPrintSettingsDto } from './receipt-print-settings.dto';

const statuses = ['active', 'inactive'] as const;

export class UpdateStoreDto {
  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  name?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  address?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  phone?: string;

  @ApiProperty({ required: false, enum: statuses })
  @IsIn(statuses)
  @IsOptional()
  status?: (typeof statuses)[number];

  @ApiProperty({
    required: false,
    description: 'Store-wide Central Online mode for all POS counters',
  })
  @IsBoolean()
  @IsOptional()
  preferCentralOnline?: boolean;

  @ApiProperty({
    required: false,
    description: 'Store-wide POS billing settings document for all counters',
  })
  @IsObject()
  @IsOptional()
  posBillingSettings?: Record<string, unknown>;

  @ApiProperty({
    required: false,
    description: 'Store-wide POS counter screen-access matrix',
  })
  @IsObject()
  @IsOptional()
  posScreenAccess?: Record<string, unknown>;

  @ApiProperty({ required: false, type: ReceiptPrintSettingsDto })
  @ValidateNested()
  @Type(() => ReceiptPrintSettingsDto)
  @IsOptional()
  receiptPrintSettings?: ReceiptPrintSettingsDto;
}
