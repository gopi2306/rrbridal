import { Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model } from 'mongoose';
import {
  AuditActor,
  diffAuditFields,
  pickAuditFields,
  serializeAuditDocument,
} from './audit-change.util';
import { AuditLog, AuditLogDocument } from './schemas/audit-log.schema';

export type LogProductChangeInput = {
  productId: string;
  sku?: string;
  action: 'created' | 'updated' | 'imported';
  before?: Record<string, unknown> | null;
  after?: Record<string, unknown> | null;
  changedFields?: string[];
  metadata?: Record<string, unknown>;
  actor?: AuditActor;
};

export type ListAuditLogsParams = {
  entityType?: string;
  entityId?: string;
  sku?: string;
  action?: string;
  page?: number;
  limit?: number;
};

@Injectable()
export class AuditLogsService {
  constructor(
    @InjectModel(AuditLog.name) private readonly auditModel: Model<AuditLogDocument>,
  ) {}

  async logProductChange(input: LogProductChangeInput): Promise<void> {
    const before = input.before ? serializeAuditDocument(input.before) : undefined;
    const after = input.after ? serializeAuditDocument(input.after) : undefined;
    const fields = input.changedFields?.filter(Boolean);
    const changes =
      before && after
        ? diffAuditFields(before, after, fields)
        : [];

    await this.auditModel.create({
      entityType: 'product',
      entityId: input.productId,
      action: input.action,
      sku: input.sku?.trim() || (typeof after?.sku === 'string' ? after.sku : undefined),
      changes,
      snapshotBefore: before,
      snapshotAfter: after,
      metadata: input.metadata,
      actorUserId: input.actor?.sub,
      actorEmail: input.actor?.email,
      actorRole: input.actor?.role,
    });
  }

  async logEvent(input: {
    entityType: string;
    entityId: string;
    action: string;
    metadata?: Record<string, unknown>;
    actor?: AuditActor;
  }): Promise<void> {
    await this.auditModel.create({
      entityType: input.entityType,
      entityId: input.entityId,
      action: input.action,
      changes: [],
      metadata: input.metadata,
      actorUserId: input.actor?.sub,
      actorEmail: input.actor?.email,
      actorRole: input.actor?.role,
    });
  }

  async list(params: ListAuditLogsParams) {
    const filter: FilterQuery<AuditLogDocument> = {};
    if (params.entityType?.trim()) filter.entityType = params.entityType.trim();
    if (params.entityId?.trim()) filter.entityId = params.entityId.trim();
    if (params.sku?.trim()) filter.sku = params.sku.trim();
    if (params.action?.trim()) filter.action = params.action.trim();

    const page = Math.max(1, params.page ?? 1);
    const limit = Math.min(200, Math.max(1, params.limit ?? 50));
    const skip = (page - 1) * limit;

    const [data, total] = await Promise.all([
      this.auditModel.find(filter).sort({ createdAt: -1 }).skip(skip).limit(limit).lean(),
      this.auditModel.countDocuments(filter),
    ]);

    return { data, total, page, limit, totalPages: Math.ceil(total / limit) };
  }

  async listProductHistory(productId: string, page = 1, limit = 50) {
    return await this.list({
      entityType: 'product',
      entityId: productId,
      page,
      limit,
    });
  }

  async listProductHistoryBySku(sku: string, page = 1, limit = 50) {
    return await this.list({
      entityType: 'product',
      sku: sku.trim(),
      page,
      limit,
    });
  }
}
