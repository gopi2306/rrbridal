import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument } from 'mongoose';

export type StorePosCounterDocument = HydratedDocument<StorePosCounter>;

@Schema({ collection: 'store_pos_counters', timestamps: true })
export class StorePosCounter {
  @Prop({ required: true, unique: true, index: true })
  key!: string;

  @Prop({ required: true, default: 0 })
  seq!: number;
}

export const StorePosCounterSchema = SchemaFactory.createForClass(StorePosCounter);
