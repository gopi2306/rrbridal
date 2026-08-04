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

  @ApiProperty({ required: false, enum: ['image', 'document'] })
  @IsIn(['image', 'document'])
  @IsOptional()
  attachmentType?: 'image' | 'document';

  @ApiProperty({ required: false, example: 'promo_broadcast', description: 'Separate from billing invoice templateName' })
  @IsString()
  @IsOptional()
  promoTemplateName?: string;

  @ApiProperty({ required: false, example: 'en' })
  @IsString()
  @IsOptional()
  promoTemplateLanguage?: string;

  @ApiProperty({ required: false, enum: ['none', 'image', 'document'] })
  @IsIn(['none', 'image', 'document'])
  @IsOptional()
  promoHeaderType?: 'none' | 'image' | 'document';

  @ApiProperty({ required: false, enum: ['none', 'name_offer', 'name_offer_date', 'name_offer_scope_date'] })
  @IsIn(['none', 'name_offer', 'name_offer_date', 'name_offer_scope_date'])
  @IsOptional()
  promoBodyParamMode?: 'none' | 'name_offer' | 'name_offer_date' | 'name_offer_scope_date';

  @ApiProperty({ required: false })
  @IsBoolean()
  @IsOptional()
  promoHasUrlButton?: boolean;
}
