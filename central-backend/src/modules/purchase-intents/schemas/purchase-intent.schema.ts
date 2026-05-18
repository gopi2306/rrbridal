import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument, Types } from 'mongoose';

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

  @ApiProperty({
    required: false,
    description: 'Stock classification label for this line (e.g. Normal Stock)',
    example: 'Normal Stock',
  })
  @Prop({ trim: true })
  stockClassification?: string;

  @ApiProperty({
    required: false,
    description: 'Destination kind hint for this line (e.g. warehouse, store)',
    example: 'warehouse',
  })
  @Prop({ trim: true })
  toKind?: string;

  @ApiProperty({
    required: false,
    description: 'Mongo _id of target Location when toLocationId is used',
  })
  @Prop({ type: Types.ObjectId })
  toLocationId?: Types.ObjectId;

  @ApiProperty({ required: false, description: 'Per-line remarks (separate from note and header remarks)' })
  @Prop({ trim: true })
  remarks?: string;
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
