import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument, Schema as MongooseSchema } from 'mongoose';

export type StoreCashMovementDocument = HydratedDocument<StoreCashMovement>;

@Schema({ collection: 'store_cash_movements', timestamps: true })
export class StoreCashMovement {
  @Prop({ required: true, index: true })
  storeId!: string;

  @Prop({ required: true, index: true })
  movementNo!: string;

  @Prop({ required: true, unique: true, index: true })
  sourceEventId!: string;

  @Prop({ required: true })
  deviceId!: string;

  @Prop({ type: MongooseSchema.Types.Mixed, required: true })
  payload!: Record<string, unknown>;
}

export const StoreCashMovementSchema = SchemaFactory.createForClass(StoreCashMovement);
StoreCashMovementSchema.index({ storeId: 1, movementNo: 1 }, { unique: true });
StoreCashMovementSchema.index({ storeId: 1, 'payload.businessDate': 1 });
