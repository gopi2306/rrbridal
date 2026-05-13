import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type PurchaseIntentDocument = HydratedDocument<PurchaseIntent>;

export type PurchaseIntentStatus =
  | 'submitted'
  | 'under_review'
  | 'approved'
  | 'rejected'
  | 'cancelled'
  | 'fulfilled';

@Schema({ _id: false })
export class PurchaseIntentLine {
  @ApiProperty()
  @Prop({ required: true })
  sku!: string;

  @ApiProperty({ required: false })
  @Prop()
  barcode?: string;

  @ApiProperty({ required: false })
  @Prop()
  description?: string;

  @ApiProperty()
  @Prop({ required: true })
  requestedQty!: number;

  @ApiProperty({ required: false })
  @Prop()
  note?: string;
}

@Schema({ timestamps: true, collection: 'purchase_intents' })
export class PurchaseIntent {
  @ApiProperty()
  @Prop({ required: true, unique: true, index: true })
  intentNo!: string;

  @ApiProperty()
  @Prop({ required: true, index: true })
  storeId!: string;

  @ApiProperty({ required: false })
  @Prop()
  deviceId?: string;

  @ApiProperty({ required: false, description: 'Set when created from store sync; equals outbox eventId' })
  @Prop({ unique: true, sparse: true, index: true })
  sourceEventId?: string;

  @ApiProperty()
  @Prop({ required: true, default: 'submitted', index: true })
  status!: PurchaseIntentStatus;

  @ApiProperty({ required: false })
  @Prop()
  remarks?: string;

  @ApiProperty({ type: [PurchaseIntentLine] })
  @Prop({ type: [PurchaseIntentLine], default: [] })
  lines!: PurchaseIntentLine[];
}

export const PurchaseIntentSchema = SchemaFactory.createForClass(PurchaseIntent);
