import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import {
  IsArray,
  IsBoolean,
  IsEmail,
  IsObject,
  IsOptional,
  IsString,
  MaxLength,
  ValidateNested,
} from 'class-validator';
import { ReceiptQrSlotDto } from './receipt-qr-slot.dto';

export class PatchCompanyProfileDto {
  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  @MaxLength(200)
  legalName?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  @MaxLength(200)
  tradeName?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  @MaxLength(20)
  gstin?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  @MaxLength(500)
  address?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  @MaxLength(100)
  city?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  @MaxLength(100)
  state?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  @MaxLength(20)
  pinCode?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  @MaxLength(40)
  phone?: string;

  @ApiProperty({ required: false })
  @IsEmail()
  @IsOptional()
  @MaxLength(200)
  email?: string;

  @ApiProperty({ required: false, description: 'URL of the company logo image' })
  @IsString()
  @IsOptional()
  @MaxLength(2000)
  companyLogo?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  @MaxLength(50)
  fssaiNo?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  @MaxLength(500)
  website?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  @MaxLength(2000)
  termsAndConditions?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  @MaxLength(200)
  thankYouLine?: string;

  @ApiProperty({ required: false, type: [String] })
  @IsArray()
  @IsString({ each: true })
  @IsOptional()
  policyLines?: string[];

  @ApiProperty({ required: false, type: [ReceiptQrSlotDto] })
  @IsArray()
  @ValidateNested({ each: true })
  @Type(() => ReceiptQrSlotDto)
  @IsOptional()
  receiptQrSlots?: ReceiptQrSlotDto[];

  @ApiProperty({ required: false })
  @IsBoolean()
  @IsOptional()
  receiptBarcodeEnabled?: boolean;

  @ApiProperty({
    required: false,
    description: 'Additional company metadata (replaces the whole map when sent)',
    additionalProperties: true,
  })
  @IsObject()
  @IsOptional()
  extraFields?: Record<string, unknown>;
}
