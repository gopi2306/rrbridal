import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type SyncEventDocument = HydratedDocument<SyncEvent>;

export type SyncEventStatus = 'applied' | 'duplicate' | 'rejected';

@Schema({ timestamps: true })
export class SyncEvent {
  @ApiProperty()
  @Prop({ required: true, unique: true, index: true })
  eventId!: string;

  @ApiProperty()
  @Prop({ required: true, index: true })
  storeId!: string;

  @ApiProperty()
  @Prop({ required: true, index: true })
  deviceId!: string;

  @ApiProperty()
  @Prop({ required: true, index: true })
  type!: string;

  @ApiProperty()
  @Prop({ required: true })
  createdAt!: string;

  @Prop({ type: Object, required: true })
  payload!: Record<string, unknown>;

  @Prop({ required: true })
  hash!: string;

  @Prop({ required: true })
  status!: SyncEventStatus;

  @Prop()
  reason?: string;
}

export const SyncEventSchema = SchemaFactory.createForClass(SyncEvent);

