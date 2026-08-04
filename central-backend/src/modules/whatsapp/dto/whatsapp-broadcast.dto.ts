import { Type } from 'class-transformer';
import {
  ArrayMaxSize,
  ArrayMinSize,
  IsArray,
  IsIn,
  IsOptional,
  IsString,
  MaxLength,
  MinLength,
  ValidateNested,
} from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';

export class WhatsAppBroadcastRecipientDto {
  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  name?: string;

  @ApiProperty()
  @IsString()
  @MinLength(8)
  phone!: string;
}

export class WhatsAppBroadcastFieldsDto {
  @ApiProperty()
  @IsString()
  storeId!: string;

  @ApiProperty({ required: false, enum: ['template', 'session'], default: 'template' })
  @IsIn(['template', 'session'])
  @IsOptional()
  mode?: 'template' | 'session';

  @ApiProperty({ description: 'Template {{2}} offer (e.g. "20") or session free-text body' })
  @IsString()
  @MinLength(1)
  @MaxLength(1000)
  promoText!: string;

  @ApiProperty({
    required: false,
    description: 'Template {{3}} scope (e.g. "All Products") — required for name_offer_scope_date',
  })
  @IsString()
  @IsOptional()
  @MaxLength(200)
  offerScope?: string;

  @ApiProperty({
    required: false,
    description: 'Template {{4}} date (e.g. "31 July 2026") — or {{3}} for older 3-param templates',
  })
  @IsString()
  @IsOptional()
  @MaxLength(120)
  offerDate?: string;

  @ApiProperty({
    required: false,
    description: 'URL button param (index 0) — full URL or path suffix, matching Meta template',
  })
  @IsString()
  @IsOptional()
  @MaxLength(500)
  urlButtonSuffix?: string;

  /** JSON string when sent as multipart form field. */
  @ApiProperty({
    required: false,
    description: 'JSON array of { name?, phone } — required for multipart; use recipientsJson or recipients',
  })
  @IsOptional()
  @IsString()
  recipientsJson?: string;

  @ApiProperty({ type: [WhatsAppBroadcastRecipientDto], required: false })
  @IsOptional()
  @IsArray()
  @ArrayMinSize(1)
  @ArrayMaxSize(5000)
  @ValidateNested({ each: true })
  @Type(() => WhatsAppBroadcastRecipientDto)
  recipients?: WhatsAppBroadcastRecipientDto[];

  @ApiProperty({
    required: false,
    type: 'string',
    format: 'binary',
    description: 'Optional promo header media when store promoHeaderType is image/document',
  })
  @IsOptional()
  attachment?: unknown;
}

/** JSON-only broadcast body — use `recipients` array (no multipart / recipientsJson). */
export class WhatsAppBroadcastJsonDto {
  @ApiProperty()
  @IsString()
  storeId!: string;

  @ApiProperty({ required: false, enum: ['template', 'session'], default: 'template' })
  @IsIn(['template', 'session'])
  @IsOptional()
  mode?: 'template' | 'session';

  @ApiProperty({ description: 'Offer / discount text (e.g. "20") or session free-text' })
  @IsString()
  @MinLength(1)
  @MaxLength(1000)
  promoText!: string;

  @ApiProperty({ required: false, description: 'Scope e.g. "All Products"' })
  @IsString()
  @IsOptional()
  @MaxLength(200)
  offerScope?: string;

  @ApiProperty({ required: false, description: 'Expiry date e.g. "31 July 2026"' })
  @IsString()
  @IsOptional()
  @MaxLength(120)
  offerDate?: string;

  @ApiProperty({ required: false, description: 'Dynamic URL button value when promoHasUrlButton is true' })
  @IsString()
  @IsOptional()
  @MaxLength(500)
  urlButtonSuffix?: string;

  @ApiProperty({ type: [WhatsAppBroadcastRecipientDto] })
  @IsArray()
  @ArrayMinSize(1)
  @ArrayMaxSize(5000)
  @ValidateNested({ each: true })
  @Type(() => WhatsAppBroadcastRecipientDto)
  recipients!: WhatsAppBroadcastRecipientDto[];
}
