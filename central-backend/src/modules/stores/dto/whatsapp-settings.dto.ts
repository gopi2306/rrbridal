import { ApiProperty } from '@nestjs/swagger';
import { IsBoolean, IsIn, IsOptional, IsString } from 'class-validator';

export class WhatsAppSettingsDto {
  @ApiProperty({ required: false })
  @IsBoolean()
  @IsOptional()
  enabled?: boolean;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  phoneNumberId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  businessAccountId?: string;

  @ApiProperty({ required: false, description: 'Omit or send blank to keep existing token' })
  @IsString()
  @IsOptional()
  accessToken?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  templateName?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  templateLanguage?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  defaultCountryCode?: string;

  @ApiProperty({ required: false, enum: ['image'] })
  @IsIn(['image'])
  @IsOptional()
  attachmentType?: 'image';
}
