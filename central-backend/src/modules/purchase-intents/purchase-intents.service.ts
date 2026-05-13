import { BadRequestException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { CreatePurchaseIntentDto } from './dto/create-purchase-intent.dto';
import { UpdatePurchaseIntentDto } from './dto/update-purchase-intent.dto';
import {
  PurchaseIntent,
  PurchaseIntentDocument,
  PurchaseIntentLine,
  PurchaseIntentStatus,
} from './schemas/purchase-intent.schema';

export type SyncEventMeta = {
  eventId: string;
  storeId: string;
  deviceId: string;
};

@Injectable()
export class PurchaseIntentsService {
  constructor(@InjectModel(PurchaseIntent.name) private readonly model: Model<PurchaseIntentDocument>) {}

  private async nextIntentNo() {
    const suffix = Math.floor(1000 + Math.random() * 9000);
    return `PINV-${suffix}`;
  }

  private parseLinesFromPayload(payload: Record<string, unknown>): PurchaseIntentLine[] {
    const raw = payload.lines;
    if (!Array.isArray(raw) || raw.length === 0) {
      throw new BadRequestException('payload.lines must be a non-empty array');
    }
    const lines: PurchaseIntentLine[] = [];
    for (const item of raw) {
      if (typeof item !== 'object' || item === null) {
        throw new BadRequestException('each line must be an object');
      }
      const o = item as Record<string, unknown>;
      const sku = o.sku;
      const requestedQty = o.requestedQty;
      if (typeof sku !== 'string' || !sku.trim()) {
        throw new BadRequestException('each line requires a non-empty sku');
      }
      if (typeof requestedQty !== 'number' || !Number.isFinite(requestedQty) || requestedQty <= 0) {
        throw new BadRequestException('each line requires requestedQty as a positive number');
      }
      const line: PurchaseIntentLine = { sku: sku.trim(), requestedQty };
      if (typeof o.barcode === 'string' && o.barcode) line.barcode = o.barcode;
      if (typeof o.description === 'string' && o.description) line.description = o.description;
      if (typeof o.note === 'string' && o.note) line.note = o.note;
      lines.push(line);
    }
    return lines;
  }

  /**
   * Idempotent: if an intent with this sourceEventId already exists (e.g. retry after sync_events write failed), returns it.
   */
  async ensureFromSync(meta: SyncEventMeta, payload: Record<string, unknown>) {
    const existing = await this.model.findOne({ sourceEventId: meta.eventId }).lean();
    if (existing) return existing;

    const lines = this.parseLinesFromPayload(payload);
    const remarks = typeof payload.remarks === 'string' ? payload.remarks : undefined;

    const intentNo = await this.nextIntentNo();
    try {
      return await this.model.create({
        intentNo,
        storeId: meta.storeId,
        deviceId: meta.deviceId,
        sourceEventId: meta.eventId,
        status: 'submitted',
        remarks,
        lines,
      });
    } catch (err: unknown) {
      const dup =
        err && typeof err === 'object' && 'code' in err && (err as { code?: number }).code === 11000;
      if (dup) {
        const again = await this.model.findOne({ sourceEventId: meta.eventId }).lean();
        if (again) return again;
      }
      throw err;
    }
  }

  async create(dto: CreatePurchaseIntentDto) {
    const intentNo = await this.nextIntentNo();
    return await this.model.create({
      intentNo,
      storeId: dto.storeId,
      deviceId: dto.deviceId,
      remarks: dto.remarks,
      status: (dto.status as PurchaseIntentStatus) ?? 'submitted',
      lines: dto.lines ?? [],
    });
  }

  async findById(id: string) {
    const doc = await this.model.findById(id).lean();
    if (!doc) throw new NotFoundException('Purchase intent not found');
    return doc;
  }

  async update(id: string, dto: UpdatePurchaseIntentDto) {
    const set: Record<string, unknown> = {};
    if (dto.status !== undefined) set.status = dto.status;
    if (dto.remarks !== undefined) set.remarks = dto.remarks;
    if (dto.lines !== undefined) set.lines = dto.lines;
    const doc = await this.model.findByIdAndUpdate(id, { $set: set }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Purchase intent not found');
    return doc;
  }

  async list(params: { search?: string; storeId?: string; status?: string }) {
    const filter: Record<string, unknown> = {};
    if (params.storeId) filter.storeId = params.storeId;
    if (params.status) filter.status = params.status;
    if (params.search) {
      filter.intentNo = { $regex: params.search, $options: 'i' };
    }
    return await this.model.find(filter).sort({ updatedAt: -1 }).limit(200).lean();
  }

  async setStatus(id: string, status: PurchaseIntentStatus) {
    const doc = await this.model.findByIdAndUpdate(id, { $set: { status } }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Purchase intent not found');
    return doc;
  }
}
