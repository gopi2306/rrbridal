import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument, Schema as MongooseSchema } from 'mongoose';

export type StoreHeldBillDocument = HydratedDocument<StoreHeldBill>;

@Schema({ collection: 'store_held_bills', timestamps: true })
export class StoreHeldBill {
  @Prop({ required: true, index: true })
  storeId!: string;

  @Prop({ required: true, index: true })
  deviceId!: string;

  @Prop({ required: true, index: true })
  holdNo!: string;

  @Prop({ type: MongooseSchema.Types.Mixed, required: true })
  payload!: Record<string, unknown>;
}

export const StoreHeldBillSchema = SchemaFactory.createForClass(StoreHeldBill);
StoreHeldBillSchema.index({ storeId: 1, holdNo: 1 }, { unique: true });
