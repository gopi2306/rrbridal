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

  @ApiProperty({ required: false, enum: ['image'], default: 'image' })
  @Prop({ trim: true, default: 'image' })
  attachmentType?: string;
}

export const WhatsAppSettingsSchema = SchemaFactory.createForClass(WhatsAppSettings);
