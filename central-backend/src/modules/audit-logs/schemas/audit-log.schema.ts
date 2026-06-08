import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument, Schema as MongooseSchema } from 'mongoose';

export type AuditLogDocument = HydratedDocument<AuditLog>;

@Schema({ _id: false })
export class AuditFieldChange {
  @Prop({ required: true })
  field!: string;

  @Prop({ type: MongooseSchema.Types.Mixed })
  before?: unknown;

  @Prop({ type: MongooseSchema.Types.Mixed })
  after?: unknown;
}

export const AuditFieldChangeSchema = SchemaFactory.createForClass(AuditFieldChange);

@Schema({ timestamps: { createdAt: true, updatedAt: false }, collection: 'audit_logs' })
export class AuditLog {
  @ApiProperty({ example: 'product' })
  @Prop({ required: true, index: true })
  entityType!: string;

  @ApiProperty()
  @Prop({ required: true, index: true })
  entityId!: string;

  @ApiProperty({ example: 'updated' })
  @Prop({ required: true, index: true })
  action!: string;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  sku?: string;

  @ApiProperty({ type: [AuditFieldChange] })
  @Prop({ type: [AuditFieldChangeSchema], default: [] })
  changes!: AuditFieldChange[];

  @Prop({ type: MongooseSchema.Types.Mixed })
  snapshotBefore?: Record<string, unknown>;

  @Prop({ type: MongooseSchema.Types.Mixed })
  snapshotAfter?: Record<string, unknown>;

  @Prop({ type: MongooseSchema.Types.Mixed })
  metadata?: Record<string, unknown>;

  @Prop()
  actorUserId?: string;

  @Prop()
  actorEmail?: string;

  @Prop()
  actorRole?: string;
}

export const AuditLogSchema = SchemaFactory.createForClass(AuditLog);
AuditLogSchema.index({ entityType: 1, entityId: 1, createdAt: -1 });
AuditLogSchema.index({ sku: 1, createdAt: -1 });
AuditLogSchema.index({ createdAt: -1 });
