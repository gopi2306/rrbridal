import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';

@Schema({ _id: false })
export class ReceiptQrSlot {
  @ApiProperty({ required: false })
  @Prop({ trim: true })
  label?: string;

  @ApiProperty({ required: false })
  @Prop({ trim: true })
  payload?: string;
}

export const ReceiptQrSlotSchema = SchemaFactory.createForClass(ReceiptQrSlot);
