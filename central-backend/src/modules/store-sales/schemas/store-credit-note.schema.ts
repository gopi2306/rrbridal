import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument } from 'mongoose';

export type StoreCreditNoteDocument = HydratedDocument<StoreCreditNote>;

export type StoreCreditNoteStatus = 'available' | 'consumed';

@Schema({ _id: false })
export class StoreCreditNoteApplication {
  @Prop({ required: true })
  billNo!: string;

  @Prop({ required: true })
  amountApplied!: number;

  @Prop()
  appliedAt?: string;

  @Prop()
  sourceEventId?: string;
}

export const StoreCreditNoteApplicationSchema = SchemaFactory.createForClass(StoreCreditNoteApplication);

@Schema({ collection: 'store_credit_notes', timestamps: true })
export class StoreCreditNote {
  @Prop({ required: true, index: true })
  storeId!: string;

  @Prop({ required: true, index: true })
  creditNoteNo!: string;

  @Prop({ required: true, unique: true, sparse: true, index: true })
  createSourceEventId?: string;

  @Prop({ required: true, default: 'available' })
  status!: StoreCreditNoteStatus;

  @Prop({ required: true })
  amount!: number;

  @Prop({ required: true })
  remainingAmount!: number;

  @Prop({ default: 0 })
  totalApplied!: number;

  @Prop()
  returnNo?: string;

  @Prop()
  originalBillNo?: string;

  @Prop()
  customerCode?: string;

  @Prop()
  customerPhone?: string;

  @Prop()
  customerName?: string;

  @Prop()
  lastAppliedBillNo?: string;

  @Prop()
  consumedBillNo?: string;

  @Prop({ type: [StoreCreditNoteApplicationSchema], default: [] })
  applications!: StoreCreditNoteApplication[];
}

export const StoreCreditNoteSchema = SchemaFactory.createForClass(StoreCreditNote);
StoreCreditNoteSchema.index({ storeId: 1, creditNoteNo: 1 }, { unique: true });
