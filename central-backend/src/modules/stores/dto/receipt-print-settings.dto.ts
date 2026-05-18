import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsBoolean, IsInt, IsOptional, IsString, Max, Min, ValidateNested } from 'class-validator';

export class ReceiptPrintSettingsDto {
  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  printerModel?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  billPrinterQueueName?: string;

  @ApiProperty({ required: false })
  @IsInt()
  @Min(32)
  @Max(56)
  @IsOptional()
  receiptCharWidth?: number;

  @ApiProperty({ required: false })
  @IsBoolean()
  @IsOptional()
  alwaysUsePrintDialog?: boolean;

  @ApiProperty({ required: false })
  @IsInt()
  @Min(58)
  @Max(120)
  @IsOptional()
  paperWidthMm?: number;
}

export class ReceiptPrintSettingsOptionalDto {
  @ApiProperty({ required: false, type: ReceiptPrintSettingsDto })
  @ValidateNested()
  @Type(() => ReceiptPrintSettingsDto)
  @IsOptional()
  receiptPrintSettings?: ReceiptPrintSettingsDto;
}
