import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument, Schema as MongooseSchema } from 'mongoose';

export type StoreQuotationDocument = HydratedDocument<StoreQuotation>;

@Schema({ collection: 'store_quotations', timestamps: true })
export class StoreQuotation {
  @Prop({ required: true, index: true })
  storeId!: string;

  @Prop({ required: true, index: true })
  quotationNo!: string;

  @Prop({ required: true, unique: true, index: true })
  sourceEventId!: string;

  @Prop({ required: true })
  deviceId!: string;

  @Prop()
  posCounter?: string;

  @Prop({ required: true, index: true })
  status!: string;

  @Prop()
  convertedBillNo?: string;

  @Prop({ type: MongooseSchema.Types.Mixed, required: true })
  payload!: Record<string, unknown>;
}

export const StoreQuotationSchema = SchemaFactory.createForClass(StoreQuotation);
StoreQuotationSchema.index({ storeId: 1, quotationNo: 1 }, { unique: true });
