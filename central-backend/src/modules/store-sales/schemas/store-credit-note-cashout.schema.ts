import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument } from 'mongoose';

export type StoreCreditNoteCashoutDocument = HydratedDocument<StoreCreditNoteCashout>;

@Schema({ collection: 'store_credit_note_cashouts', timestamps: true })
export class StoreCreditNoteCashout {
  @Prop({ required: true, index: true })
  storeId!: string;

  @Prop({ required: true, index: true })
  cashoutNo!: string;

  @Prop({ required: true, unique: true, sparse: true, index: true })
  createSourceEventId?: string;

  @Prop({ required: true })
  creditNoteNo!: string;

  @Prop()
  billNo?: string;

  @Prop({ required: true })
  cashRefunded!: number;

  @Prop({ required: true })
  remainingBefore!: number;

  @Prop({ required: true })
  remainingAfter!: number;

  @Prop()
  posCounter?: string;

  @Prop()
  customerCode?: string;

  @Prop()
  customerPhone?: string;

  @Prop()
  customerName?: string;

  @Prop({ required: true, default: 'posted' })
  status!: string;

  @Prop()
  createdAtUtc?: string;
}

export const StoreCreditNoteCashoutSchema = SchemaFactory.createForClass(StoreCreditNoteCashout);
StoreCreditNoteCashoutSchema.index({ storeId: 1, cashoutNo: 1 }, { unique: true });
