import { IsNumber, IsOptional, IsString } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';

export class WhatsAppSettingsQueryDto {
  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  storeId?: string;
}

export class SendWhatsAppInvoiceFieldsDto {
  @ApiProperty()
  @IsString()
  storeId!: string;

  @ApiProperty()
  @IsString()
  billNo!: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  customerName?: string;

  @ApiProperty()
  @IsString()
  customerPhone!: string;

  @ApiProperty()
  @Type(() => Number)
  @IsNumber()
  payable!: number;
}

export class WhatsAppTestSendDto {
  @ApiProperty({ required: false, description: 'Store code; defaults to first active store' })
  @IsString()
  @IsOptional()
  storeId?: string;

  @ApiProperty({ description: 'Destination mobile (10-digit or E.164)' })
  @IsString()
  customerPhone!: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  customerName?: string;

  @ApiProperty({
    required: false,
    type: 'string',
    format: 'binary',
    description:
      'Optional PDF or PNG. If omitted, a sample file is generated from the store attachmentType.',
  })
  @IsOptional()
  attachment?: unknown;
}
