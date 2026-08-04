import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { Schema as MongooseSchema } from 'mongoose';

@Schema({ _id: false })
export class ReceiptPrintSettings {
  @ApiProperty({ required: false, example: 'TVS RP 3200 Lite' })
  @Prop({ trim: true })
  printerModel?: string;

  @ApiProperty({ required: false, example: 'TVS RP 3200' })
  @Prop({ trim: true })
  billPrinterQueueName?: string;

  @ApiProperty({ required: false, example: 48 })
  @Prop({ min: 32, max: 56 })
  receiptCharWidth?: number;

  @ApiProperty({ required: false, default: false })
  @Prop({ default: false })
  alwaysUsePrintDialog?: boolean;

  @ApiProperty({ required: false, example: 80 })
  @Prop({ min: 58, max: 120 })
  paperWidthMm?: number;

  @ApiProperty({ required: false, example: 'Thermal', description: 'Thermal | A4 | A5 | A4Commercial' })
  @Prop({ trim: true })
  printFormat?: string;

  @ApiProperty({ required: false, example: 'Thermal', description: 'Thermal | A4' })
  @Prop({ trim: true })
  creditPrintFormat?: string;

  @ApiProperty({ required: false, default: false })
  @Prop({ default: false })
  a4PrePrintedEnabled?: boolean;

  @ApiProperty({ required: false, default: false })
  @Prop({ default: false })
  a5PrePrintedEnabled?: boolean;

  @ApiProperty({ required: false, default: false })
  @Prop({ default: false })
  alsoPrintThermalFirst?: boolean;

  @ApiProperty({ required: false, description: 'A4 pre-printed mm/font layout (store-wide)' })
  @Prop({ type: MongooseSchema.Types.Mixed })
  a4PrePrintedLayout?: Record<string, unknown>;

  @ApiProperty({ required: false, description: 'A5 pre-printed mm/font layout (store-wide)' })
  @Prop({ type: MongooseSchema.Types.Mixed })
  a5PrePrintedLayout?: Record<string, unknown>;
}

export const ReceiptPrintSettingsSchema = SchemaFactory.createForClass(ReceiptPrintSettings);
