import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument } from 'mongoose';

export type WhatsAppBroadcastJobDocument = HydratedDocument<WhatsAppBroadcastJob>;

@Schema({ _id: false })
export class WhatsAppBroadcastRecipient {
  @Prop({ required: true, trim: true })
  name!: string;

  @Prop({ required: true, trim: true })
  phone!: string;

  @Prop({ trim: true })
  phoneE164?: string;

  @Prop({ trim: true, default: 'pending' })
  status?: string;

  @Prop({ trim: true })
  messageId?: string;

  /** Meta webhook: sent | delivered | read | failed (after API accept). */
  @Prop({ trim: true })
  deliveryStatus?: string;

  @Prop({ trim: true })
  deliveryError?: string;

  @Prop()
  deliveryAt?: Date;

  @Prop({ trim: true })
  error?: string;
}

export const WhatsAppBroadcastRecipientSchema = SchemaFactory.createForClass(WhatsAppBroadcastRecipient);

@Schema({ timestamps: true, collection: 'whatsapp_broadcast_jobs' })
export class WhatsAppBroadcastJob {
  @Prop({ required: true, trim: true, lowercase: true, index: true })
  storeId!: string;

  @Prop({ trim: true, default: 'template' })
  mode!: string;

  /** Template {{2}} / session free-text offer line (e.g. "20"). */
  @Prop({ required: true, trim: true })
  promoText!: string;

  /** Template {{3}} — e.g. "All Products". */
  @Prop({ trim: true, default: '' })
  offerScope!: string;

  /** Template {{4}} (or {{3}} on older templates) — e.g. "31 July 2026". */
  @Prop({ trim: true, default: '' })
  offerDate!: string;

  /** URL button param (index 0) — full URL or suffix per Meta template. */
  @Prop({ trim: true, default: '' })
  urlButtonSuffix!: string;

  @Prop({ trim: true, default: 'queued', index: true })
  status!: string;

  @Prop({ type: [WhatsAppBroadcastRecipientSchema], default: [] })
  recipients!: WhatsAppBroadcastRecipient[];

  @Prop({ default: 0 })
  total!: number;

  @Prop({ default: 0 })
  sent!: number;

  @Prop({ default: 0 })
  failed!: number;

  @Prop({ default: 0 })
  skipped!: number;

  @Prop({ trim: true })
  mediaId?: string;

  @Prop({ trim: true })
  mediaFilename?: string;

  @Prop({ trim: true })
  mediaMimeType?: string;

  @Prop()
  startedAt?: Date;

  @Prop()
  finishedAt?: Date;

  @Prop({ trim: true })
  error?: string;
}

export const WhatsAppBroadcastJobSchema = SchemaFactory.createForClass(WhatsAppBroadcastJob);
