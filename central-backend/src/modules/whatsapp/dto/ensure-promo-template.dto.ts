import { ApiProperty } from '@nestjs/swagger';
import { IsOptional, IsString } from 'class-validator';

export class EnsurePromoTemplateDto {
  @ApiProperty({ example: 'store-001' })
  @IsString()
  storeId!: string;

  @ApiProperty({
    required: false,
    description: 'WhatsApp Business Account ID from Meta API Setup (overrides store businessAccountId)',
  })
  @IsString()
  @IsOptional()
  wabaId?: string;

  @ApiProperty({ required: false, example: 'promo_offer' })
  @IsString()
  @IsOptional()
  templateName?: string;

  @ApiProperty({ required: false, example: 'en' })
  @IsString()
  @IsOptional()
  language?: string;

  @ApiProperty({ required: false, example: 'https://rrstyle.rrbazaar.in/p/' })
  @IsString()
  @IsOptional()
  urlBase?: string;
}
