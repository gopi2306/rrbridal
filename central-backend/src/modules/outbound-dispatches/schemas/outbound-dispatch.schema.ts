import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument, Schema as MongooseSchema } from 'mongoose';

export const OUTBOUND_DISPATCH_STATUSES = [
  'Draft',
  'Ready',
  'HandedOver',
  'Delivered',
  'Cancelled',
  'Returned',
] as const;

export type OutboundDispatchStatus = (typeof OUTBOUND_DISPATCH_STATUSES)[number];
export type OutboundDispatchDocument = HydratedDocument<OutboundDispatch>;

@Schema({ collection: 'outbound_dispatches', timestamps: true })
export class OutboundDispatch {
  @Prop({ required: true, index: true })
  dispatchNo!: string;

  @Prop({ index: true })
  batchNo?: string;

  @Prop({ required: true, index: true })
  storeCode!: string;

  @Prop({ required: true })
  deviceId!: string;

  @Prop()
  posCounter?: string;

  @Prop({ required: true, index: true })
  businessDate!: string;

  @Prop({ required: true, index: true })
  billNo!: string;

  @Prop({ type: MongooseSchema.Types.Mixed, required: true })
  billSnapshot!: Record<string, unknown>;

  @Prop({ type: [MongooseSchema.Types.Mixed], default: [] })
  lines!: Record<string, unknown>[];

  @Prop({ type: MongooseSchema.Types.Mixed, required: true })
  shipTo!: Record<string, unknown>;

  @Prop({ required: true })
  carrierType!: string;

  @Prop({ type: MongooseSchema.Types.Mixed, required: true })
  carrier!: Record<string, unknown>;

  @Prop()
  carrierName?: string;

  @Prop()
  trackingNo?: string;

  @Prop({ required: true, min: 1, default: 1 })
  packageCount!: number;

  @Prop({ required: true, enum: ['customer', 'store'] })
  feePayer!: 'customer' | 'store';

  @Prop({ required: true, min: 0, default: 0 })
  dispatchFee!: number;

  @Prop({ type: MongooseSchema.Types.Mixed, required: true })
  fee!: Record<string, unknown>;

  @Prop()
  feePaymentMode?: string;

  @Prop()
  feePaymentReference?: string;

  @Prop()
  chargeReceiptNo?: string;

  @Prop()
  expenseNo?: string;

  @Prop({ type: MongooseSchema.Types.Mixed })
  chargeReceipt?: Record<string, unknown>;

  @Prop({ type: MongooseSchema.Types.Mixed })
  expense?: Record<string, unknown>;

  @Prop({ type: MongooseSchema.Types.Mixed })
  linkedExpense?: Record<string, unknown>;

  @Prop({ required: true, enum: OUTBOUND_DISPATCH_STATUSES, index: true })
  status!: OutboundDispatchStatus;

  @Prop({ required: true, default: true, index: true })
  active!: boolean;

  @Prop({ type: [MongooseSchema.Types.Mixed], default: [] })
  audit!: Record<string, unknown>[];

  @Prop({ type: [MongooseSchema.Types.Mixed], default: [] })
  auditHistory!: Record<string, unknown>[];

  @Prop({ required: true, min: 1, default: 1 })
  revision!: number;

  @Prop({ type: [String], default: [] })
  appliedEventIds!: string[];

  @Prop({ required: true })
  createSourceEventId!: string;

  @Prop()
  updatedAtUtc?: string;

  @Prop()
  cancelledAtUtc?: string;
}

export const OutboundDispatchSchema = SchemaFactory.createForClass(OutboundDispatch);
OutboundDispatchSchema.index({ storeCode: 1, dispatchNo: 1 }, { unique: true });
OutboundDispatchSchema.index({ createSourceEventId: 1 }, { unique: true });
OutboundDispatchSchema.index({ storeCode: 1, businessDate: 1, status: 1, batchNo: 1 });
OutboundDispatchSchema.index(
  { storeCode: 1, billNo: 1 },
  {
    unique: true,
    partialFilterExpression: {
      status: { $in: ['Draft', 'Ready', 'HandedOver'] },
    },
    name: 'one_active_dispatch_per_bill',
  },
);
