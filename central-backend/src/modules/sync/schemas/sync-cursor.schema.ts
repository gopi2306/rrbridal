import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument } from 'mongoose';

export type SyncCursorDocument = HydratedDocument<SyncCursor>;

@Schema({ timestamps: true })
export class SyncCursor {
  @Prop({ required: true, unique: true, index: true })
  storeId!: string;

  @Prop({ required: true, default: '0' })
  cursor!: string;

  @Prop()
  lastSuccessAt?: string;
}

export const SyncCursorSchema = SchemaFactory.createForClass(SyncCursor);

