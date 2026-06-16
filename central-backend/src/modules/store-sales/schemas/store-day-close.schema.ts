import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument, Schema as MongooseSchema } from 'mongoose';

export type StoreDayCloseDocument = HydratedDocument<StoreDayClose>;

@Schema({ collection: 'store_day_closes', timestamps: true })
export class StoreDayClose {
  @Prop({ required: true, index: true })
  storeId!: string;

  @Prop({ required: true, index: true })
  businessDate!: string;

  @Prop({ required: true, index: true })
  posCounter!: string;

  @Prop({ required: true, unique: true, index: true })
  sourceEventId!: string;

  @Prop({ required: true })
  deviceId!: string;

  @Prop({ type: MongooseSchema.Types.Mixed, required: true })
  payload!: Record<string, unknown>;
}

export const StoreDayCloseSchema = SchemaFactory.createForClass(StoreDayClose);
StoreDayCloseSchema.index({ storeId: 1, businessDate: 1, posCounter: 1 }, { unique: true });
StoreDayCloseSchema.index({ storeId: 1, businessDate: 1 });
