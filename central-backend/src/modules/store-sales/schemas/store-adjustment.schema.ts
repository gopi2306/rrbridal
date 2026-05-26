import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument, Schema as MongooseSchema } from 'mongoose';

export type StoreAdjustmentDocument = HydratedDocument<StoreAdjustment>;

@Schema({ collection: 'store_adjustments', timestamps: true })
export class StoreAdjustment {
  @Prop({ required: true, index: true })
  storeId!: string;

  @Prop({ required: true, index: true })
  adjustmentNo!: string;

  @Prop({ required: true, unique: true, index: true })
  sourceEventId!: string;

  @Prop({ required: true })
  deviceId!: string;

  @Prop({ type: MongooseSchema.Types.Mixed, required: true })
  payload!: Record<string, unknown>;
}

export const StoreAdjustmentSchema = SchemaFactory.createForClass(StoreAdjustment);
StoreAdjustmentSchema.index({ storeId: 1, adjustmentNo: 1 }, { unique: true });
