import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument, Schema as MongooseSchema } from 'mongoose';

export type StoreDailyExpenseDocument = HydratedDocument<StoreDailyExpense>;

@Schema({ collection: 'store_daily_expenses', timestamps: true })
export class StoreDailyExpense {
  @Prop({ required: true, index: true })
  storeId!: string;

  @Prop({ required: true, index: true })
  expenseNo!: string;

  @Prop({ required: true, unique: true, index: true })
  sourceEventId!: string;

  @Prop({ required: true })
  deviceId!: string;

  @Prop({ type: MongooseSchema.Types.Mixed, required: true })
  payload!: Record<string, unknown>;
}

export const StoreDailyExpenseSchema = SchemaFactory.createForClass(StoreDailyExpense);
StoreDailyExpenseSchema.index({ storeId: 1, expenseNo: 1 }, { unique: true });
StoreDailyExpenseSchema.index({ storeId: 1, 'payload.businessDate': 1 });
