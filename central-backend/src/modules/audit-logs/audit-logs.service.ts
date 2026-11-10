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

export function createdAtRange(from?: string, to?: string): { $gte?: Date; $lte?: Date } | undefined {
  const gte = parseRangeBound(from, 'start');
  const lte = parseRangeBound(to, 'end');
  if (!gte && !lte) return undefined;
  const range: { $gte?: Date; $lte?: Date } = {};
  if (gte) range.$gte = gte;
  if (lte) range.$lte = lte;
  return range;
}

function parseRangeBound(value: string | undefined, edge: 'start' | 'end'): Date | undefined {
  const trimmed = value?.trim();
  if (!trimmed) return undefined;
  const dateOnly = /^(\d{4})-(\d{2})-(\d{2})$/.exec(trimmed);
  if (dateOnly) {
    const year = Number(dateOnly[1]);
    const month = Number(dateOnly[2]);
    const day = Number(dateOnly[3]);
    if (edge === 'start') return new Date(Date.UTC(year, month - 1, day, 0, 0, 0, 0));
    return new Date(Date.UTC(year, month - 1, day, 23, 59, 59, 999));
  }
  const parsed = new Date(trimmed);
  if (Number.isNaN(parsed.getTime())) return undefined;
  return parsed;
}

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
  source?: string;
  storeId?: string;
  deviceId?: string;
  from?: string;
  to?: string;
  billNo?: string;
  posCounter?: string;
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
    sku?: string;
    metadata?: Record<string, unknown>;
    actor?: AuditActor;
    source?: string;
    storeId?: string;
    deviceId?: string;
    billNo?: string;
    posCounter?: string;
  }): Promise<void> {
    await this.auditModel.create({
      entityType: input.entityType,
      entityId: input.entityId,
      action: input.action,
      sku: input.sku?.trim() || undefined,
      changes: [],
      metadata: input.metadata,
      actorUserId: input.actor?.sub,
      actorEmail: input.actor?.email,
      actorRole: input.actor?.role,
      source: input.source?.trim() || undefined,
      storeId: input.storeId?.trim() || undefined,
      deviceId: input.deviceId?.trim() || undefined,
      billNo: input.billNo?.trim() || undefined,
      posCounter: input.posCounter?.trim() || undefined,
    });
  }

  async list(params: ListAuditLogsParams) {
    const filter: FilterQuery<AuditLogDocument> = {};
    if (params.entityType?.trim()) filter.entityType = params.entityType.trim();
    if (params.entityId?.trim()) filter.entityId = params.entityId.trim();
    if (params.sku?.trim()) filter.sku = params.sku.trim();
    if (params.action?.trim()) filter.action = params.action.trim();
    if (params.source?.trim()) filter.source = params.source.trim();
    if (params.storeId?.trim()) filter.storeId = params.storeId.trim();
    if (params.deviceId?.trim()) filter.deviceId = params.deviceId.trim();
    if (params.billNo?.trim()) filter.billNo = params.billNo.trim();
    if (params.posCounter?.trim()) filter.posCounter = params.posCounter.trim();
    const createdAt = createdAtRange(params.from, params.to);
    if (createdAt) filter.createdAt = createdAt;

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
