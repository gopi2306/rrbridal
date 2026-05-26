import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument, Schema as MongooseSchema } from 'mongoose';

export type StoreSaleReturnDocument = HydratedDocument<StoreSaleReturn>;

@Schema({ collection: 'store_sale_returns', timestamps: true })
export class StoreSaleReturn {
  @Prop({ required: true, index: true })
  storeId!: string;

  @Prop({ required: true, index: true })
  returnNo!: string;

  @Prop({ required: true, unique: true, index: true })
  sourceEventId!: string;

  @Prop({ required: true })
  deviceId!: string;

  @Prop({ required: true })
  kind!: 'return' | 'exchange';

  @Prop({ type: MongooseSchema.Types.Mixed, required: true })
  payload!: Record<string, unknown>;
}

export const StoreSaleReturnSchema = SchemaFactory.createForClass(StoreSaleReturn);
StoreSaleReturnSchema.index({ storeId: 1, returnNo: 1 }, { unique: true });
