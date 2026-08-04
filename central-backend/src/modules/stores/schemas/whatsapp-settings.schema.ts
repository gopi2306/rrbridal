import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';

@Schema({ _id: false })
export class WhatsAppSettings {
  @ApiProperty({ required: false, default: false })
  @Prop({ default: false })
  enabled?: boolean;

  @ApiProperty({ required: false })
  @Prop({ trim: true })
  phoneNumberId?: string;

  @ApiProperty({ required: false })
  @Prop({ trim: true })
  businessAccountId?: string;

  @ApiProperty({ required: false, description: 'Stored server-side; never returned in full to clients' })
  @Prop({ trim: true })
  accessToken?: string;

  @ApiProperty({ required: false, example: 'invoice_delivery' })
  @Prop({ trim: true })
  templateName?: string;

  @ApiProperty({ required: false, example: 'en' })
  @Prop({ trim: true, default: 'en' })
  templateLanguage?: string;

  @ApiProperty({ required: false, example: '91' })
  @Prop({ trim: true, default: '91' })
  defaultCountryCode?: string;

  @ApiProperty({ required: false, enum: ['image', 'document'], default: 'image' })
  @Prop({ trim: true, default: 'image' })
  attachmentType?: string;

  @ApiProperty({ required: false, example: 'promo_broadcast', description: 'Marketing template for customer broadcast (separate from billing invoice template)' })
  @Prop({ trim: true })
  promoTemplateName?: string;

  @ApiProperty({ required: false, example: 'en' })
  @Prop({ trim: true, default: 'en' })
  promoTemplateLanguage?: string;

  @ApiProperty({ required: false, enum: ['none', 'image', 'document'], default: 'none' })
  @Prop({ trim: true, default: 'none' })
  promoHeaderType?: string;

  /**
   * How many body variables the Meta promo template expects:
   * - none: static body (0 vars)
   * - name_offer: {{1}} name, {{2}} offer
   * - name_offer_date: {{1}} name, {{2}} offer, {{3}} date
   * - name_offer_scope_date: {{1}} name, {{2}} offer, {{3}} scope, {{4}} date (promo_offer)
   */
  @ApiProperty({
    required: false,
    enum: ['none', 'name_offer', 'name_offer_date', 'name_offer_scope_date'],
    default: 'name_offer_scope_date',
  })
  @Prop({ trim: true, default: 'name_offer_scope_date' })
  promoBodyParamMode?: string;

  /** When true, send dynamic URL button index 0 parameter (template must have a dynamic URL). */
  @ApiProperty({ required: false, default: false })
  @Prop({ default: false })
  promoHasUrlButton?: boolean;
}

export const WhatsAppSettingsSchema = SchemaFactory.createForClass(WhatsAppSettings);
