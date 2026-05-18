import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsIn, IsOptional, IsString, ValidateNested } from 'class-validator';
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

  @ApiProperty({ required: false, type: ReceiptPrintSettingsDto })
  @ValidateNested()
  @Type(() => ReceiptPrintSettingsDto)
  @IsOptional()
  receiptPrintSettings?: ReceiptPrintSettingsDto;
}
