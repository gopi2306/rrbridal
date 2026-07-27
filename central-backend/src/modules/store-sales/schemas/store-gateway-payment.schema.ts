import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument, Schema as MongooseSchema } from 'mongoose';

export type StoreGatewayPaymentDocument = HydratedDocument<StoreGatewayPayment>;

@Schema({ collection: 'store_gateway_payments', timestamps: true })
export class StoreGatewayPayment {
  @Prop({ required: true, index: true })
  storeId!: string;

  @Prop({ required: true, index: true })
  invoiceNo!: string;

  @Prop({ required: true, unique: true, index: true })
  sourceEventId!: string;

  @Prop({ required: true })
  deviceId!: string;

  @Prop({ index: true })
  posCounter?: string;

  @Prop({ type: MongooseSchema.Types.Mixed, required: true })
  payload!: Record<string, unknown>;
}

export const StoreGatewayPaymentSchema = SchemaFactory.createForClass(StoreGatewayPayment);
StoreGatewayPaymentSchema.index({ storeId: 1, createdAt: -1 });
