import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';
import { Schema as MongooseSchema } from 'mongoose';
import { ReceiptPrintSettings, ReceiptPrintSettingsSchema } from './receipt-print-settings.schema';
import { WhatsAppSettings, WhatsAppSettingsSchema } from './whatsapp-settings.schema';

export type StoreDocument = HydratedDocument<Store>;
export type StoreStatus = 'active' | 'inactive';

@Schema({ timestamps: true, collection: 'stores' })
export class Store {
  @ApiProperty({ example: 'store-001' })
  @Prop({ required: true, unique: true, trim: true, lowercase: true, index: true })
  code!: string;

  @ApiProperty({ example: 'RR Bridal - Main Branch' })
  @Prop({ required: true, trim: true })
  name!: string;

  @ApiProperty({ required: false })
  @Prop({ trim: true })
  address?: string;

  @ApiProperty({ required: false })
  @Prop({ trim: true })
  phone?: string;

  @ApiProperty({ enum: ['active', 'inactive'], default: 'active' })
  @Prop({ required: true, default: 'active', index: true })
  status!: StoreStatus;

  @ApiProperty({
    required: false,
    default: false,
    description:
      'When true, all POS counters use Central Online mode (direct central API; no shared parent Mongo / ZeroTier).',
  })
  @Prop({ required: true, default: false, index: true })
  preferCentralOnline!: boolean;

  @ApiProperty({
    required: false,
    description: 'Store-wide POS billing settings (duplicate print, credit rules, screen access, etc.).',
  })
  @Prop({ type: MongooseSchema.Types.Mixed })
  posBillingSettings?: Record<string, unknown>;

  @ApiProperty({
    required: false,
    description: 'Store-wide POS counter screen-access matrix (same on every till).',
  })
  @Prop({ type: MongooseSchema.Types.Mixed })
  posScreenAccess?: Record<string, unknown>;

  @ApiProperty({ required: false, type: ReceiptPrintSettings })
  @Prop({ type: ReceiptPrintSettingsSchema })
  receiptPrintSettings?: ReceiptPrintSettings;

  @ApiProperty({ required: false, type: WhatsAppSettings })
  @Prop({ type: WhatsAppSettingsSchema })
  whatsappSettings?: WhatsAppSettings;
}

export const StoreSchema = SchemaFactory.createForClass(Store);
