import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument, Schema as MongooseSchema } from 'mongoose';

export type StoreInvoiceDocument = HydratedDocument<StoreInvoice>;

@Schema({ collection: 'store_invoices', timestamps: true })
export class StoreInvoice {
  @Prop({ required: true, index: true })
  storeId!: string;

  @Prop({ required: true, index: true })
  invoiceNo!: string;

  @Prop({ required: true, unique: true, index: true })
  sourceEventId!: string;

  @Prop({ required: true })
  deviceId!: string;

  @Prop()
  posCounter?: string;

  @Prop({ type: MongooseSchema.Types.Mixed, required: true })
  payload!: Record<string, unknown>;
}

export const StoreInvoiceSchema = SchemaFactory.createForClass(StoreInvoice);
StoreInvoiceSchema.index({ storeId: 1, invoiceNo: 1 }, { unique: true });
