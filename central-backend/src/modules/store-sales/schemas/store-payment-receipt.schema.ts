import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument, Schema as MongooseSchema } from 'mongoose';

export type StorePaymentReceiptDocument = HydratedDocument<StorePaymentReceipt>;

@Schema({ collection: 'store_payment_receipts', timestamps: true })
export class StorePaymentReceipt {
  @Prop({ required: true, index: true })
  storeId!: string;

  @Prop({ required: true, index: true })
  receiptNo!: string;

  @Prop({ required: true, index: true })
  billNo!: string;

  @Prop({ required: true, unique: true, index: true })
  sourceEventId!: string;

  @Prop({ required: true })
  deviceId!: string;

  @Prop({ type: MongooseSchema.Types.Mixed, required: true })
  payload!: Record<string, unknown>;
}

export const StorePaymentReceiptSchema = SchemaFactory.createForClass(StorePaymentReceipt);
StorePaymentReceiptSchema.index({ storeId: 1, receiptNo: 1 }, { unique: true });
