import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';

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
}

export const ReceiptPrintSettingsSchema = SchemaFactory.createForClass(ReceiptPrintSettings);
