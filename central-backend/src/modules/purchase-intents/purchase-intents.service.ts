import { BadRequestException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model, SortOrder, Types } from 'mongoose';
import { DocumentNumberService } from '../../common/document-number.service';
import { attachLineProducts, enrichDocWithLineProducts, resolveProductIdForSku } from '../../common/product-line-enrichment';
import { DocumentNumberAllocatorService } from '../document-numbers/document-number-allocator.service';
import { DocumentNumberConfigService } from '../document-numbers/document-number-config.service';
import { Product, ProductDocument } from '../products/schemas/product.schema';
import { CreatePurchaseIntentLineDto } from './dto/create-purchase-intent-line.dto';
import { CreatePurchaseIntentDto } from './dto/create-purchase-intent.dto';
import { FilterPurchaseIntentDto } from './dto/filter-purchase-intent.dto';
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
  constructor(
    @InjectModel(PurchaseIntent.name) private readonly model: Model<PurchaseIntentDocument>,
    @InjectModel(Product.name) private readonly productModel: Model<ProductDocument>,
    private readonly allocator: DocumentNumberAllocatorService,
    private readonly configService: DocumentNumberConfigService,
  ) {}

  private async allocateIntentNo(): Promise<string> {
    const config = await this.configService.getByKey('purchase_intent');
    const prefix = config.prefix;
    return this.allocator.allocate('purchase_intent', {
      exists: async (v) => !!(await this.model.exists({ intentNo: v }).lean()),
      syncFloorFromValues: () => this.maxIntentSequenceForPrefix(prefix),
    });
  }

  private async maxIntentSequenceForPrefix(prefix: string): Promise<number> {
    const escaped = prefix.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const regex = new RegExp(`^${escaped}\\d+$`, 'i');
    const rows = await this.model.find({ intentNo: regex }).select('intentNo').lean();
    let max = 0;
    for (const row of rows) {
      if (typeof row.intentNo !== 'string') continue;
      const n = DocumentNumberService.parseSequenceNumber(row.intentNo, prefix);
      if (n !== null && n > max) max = n;
    }
    return max;
  }

  private normalizeLineStockClassification(value: string | undefined): string | undefined {
    const t = value?.trim();
    if (!t) return undefined;
    return t.length > 80 ? t.slice(0, 80) : t;
  }

  private normalizeLineToKind(value: string | undefined): string | undefined {
    const t = value?.trim();
    if (!t) return undefined;
    return t.length > 40 ? t.slice(0, 40) : t;
  }

  private normalizeLineRemarks(value: string | undefined): string | undefined {
    const t = value?.trim();
    if (!t) return undefined;
    return t.length > 500 ? t.slice(0, 500) : t;
  }

  private parseToLocationIdFromUnknown(value: unknown): Types.ObjectId | undefined {
    if (value === undefined || value === null || value === '') return undefined;
    const id = typeof value === 'string' ? value.trim() : String(value).trim();
    if (!id) return undefined;
    if (!Types.ObjectId.isValid(id)) {
      throw new BadRequestException('each line toLocationId must be a valid Mongo ObjectId when provided');
    }
    return new Types.ObjectId(id);
  }

  private enrichLineFromRecord(line: PurchaseIntentLine, o: Record<string, unknown>): void {
    const sc =
      typeof o.stockClassification === 'string'
        ? this.normalizeLineStockClassification(o.stockClassification)
        : undefined;
    if (sc) line.stockClassification = sc;
    const tk = typeof o.toKind === 'string' ? this.normalizeLineToKind(o.toKind) : undefined;
    if (tk) line.toKind = tk;
    const loc = this.parseToLocationIdFromUnknown(o.toLocationId);
    if (loc) line.toLocationId = loc;
    const rmk = typeof o.remarks === 'string' ? this.normalizeLineRemarks(o.remarks) : undefined;
    if (rmk) line.remarks = rmk;
  }

  private async normalizeDtoLine(dto: CreatePurchaseIntentLineDto): Promise<PurchaseIntentLine> {
    const sku = dto.sku.trim();
    const productId = await resolveProductIdForSku(this.productModel, sku, dto.productId);
    const line: PurchaseIntentLine = { sku, requestedQty: dto.requestedQty };
    if (productId) line.productId = productId;
    if (dto.barcode?.trim()) line.barcode = dto.barcode.trim();
    if (dto.description?.trim()) line.description = dto.description.trim();
    if (dto.note?.trim()) line.note = dto.note.trim();
    const sc = this.normalizeLineStockClassification(dto.stockClassification);
    if (sc) line.stockClassification = sc;
    const tk = this.normalizeLineToKind(dto.toKind);
    if (tk) line.toKind = tk;
    if (dto.toLocationId?.trim()) line.toLocationId = new Types.ObjectId(dto.toLocationId.trim());
    const rmk = this.normalizeLineRemarks(dto.remarks);
    if (rmk) line.remarks = rmk;
    return line;
  }

  private async normalizeLineFromRecord(
    sku: string,
    requestedQty: number,
    o: Record<string, unknown>,
  ): Promise<PurchaseIntentLine> {
    const rawProductId =
      typeof o.productId === 'string'
        ? o.productId
        : o.productId != null
          ? String(o.productId)
          : undefined;
    const productId = await resolveProductIdForSku(this.productModel, sku, rawProductId);
    const line: PurchaseIntentLine = { sku, requestedQty };
    if (productId) line.productId = productId;
    if (typeof o.barcode === 'string' && o.barcode) line.barcode = o.barcode;
    if (typeof o.description === 'string' && o.description) line.description = o.description;
    if (typeof o.note === 'string' && o.note) line.note = o.note;
    this.enrichLineFromRecord(line, o);
    return line;
  }

  private async parseLinesFromPayload(payload: Record<string, unknown>): Promise<PurchaseIntentLine[]> {
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
      lines.push(await this.normalizeLineFromRecord(sku.trim(), requestedQty, o));
    }
    return lines;
  }

  /**
   * Idempotent: if an intent with this sourceEventId already exists (e.g. retry after sync_events write failed), returns it.
   */
  async ensureFromSync(meta: SyncEventMeta, payload: Record<string, unknown>) {
    const existing = await this.model.findOne({ sourceEventId: meta.eventId }).lean();
    if (existing) return await enrichDocWithLineProducts(this.productModel, existing as unknown as Record<string, unknown>);

    const lines = await this.parseLinesFromPayload(payload);
    const remarks = typeof payload.remarks === 'string' ? payload.remarks : undefined;

    const intentNo = await this.allocateIntentNo();
    try {
      const created = await this.model.create({
        intentNo,
        storeId: meta.storeId,
        deviceId: meta.deviceId,
        sourceEventId: meta.eventId,
        status: 'submitted',
        remarks,
        lines,
      });
      return await enrichDocWithLineProducts(this.productModel, created.toObject() as unknown as Record<string, unknown>);
    } catch (err: unknown) {
      const dup =
        err && typeof err === 'object' && 'code' in err && (err as { code?: number }).code === 11000;
      if (dup) {
        const again = await this.model.findOne({ sourceEventId: meta.eventId }).lean();
        if (again) return await enrichDocWithLineProducts(this.productModel, again as unknown as Record<string, unknown>);
      }
      throw err;
    }
  }

  async create(dto: CreatePurchaseIntentDto) {
    const intentNo = await this.allocateIntentNo();
    const lines: PurchaseIntentLine[] = [];
    for (const l of dto.lines ?? []) {
      lines.push(await this.normalizeDtoLine(l));
    }
    const created = await this.model.create({
      intentNo,
      storeId: dto.storeId,
      deviceId: dto.deviceId,
      remarks: dto.remarks,
      status: (dto.status as PurchaseIntentStatus) ?? 'submitted',
      lines,
    });
    return await enrichDocWithLineProducts(this.productModel, created.toObject() as unknown as Record<string, unknown>);
  }

  async findById(id: string) {
    const doc = await this.model.findById(id).lean();
    if (!doc) throw new NotFoundException('Purchase intent not found');
    return await enrichDocWithLineProducts(this.productModel, doc as unknown as Record<string, unknown>);
  }

  async update(id: string, dto: UpdatePurchaseIntentDto) {
    const set: Record<string, unknown> = {};
    if (dto.status !== undefined) set.status = dto.status;
    if (dto.remarks !== undefined) set.remarks = dto.remarks;
    if (dto.lines !== undefined) {
      const lines: PurchaseIntentLine[] = [];
      for (const l of dto.lines) {
        lines.push(await this.normalizeDtoLine(l));
      }
      set.lines = lines;
    }
    const doc = await this.model.findByIdAndUpdate(id, { $set: set }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Purchase intent not found');
    return await enrichDocWithLineProducts(this.productModel, doc as unknown as Record<string, unknown>);
  }

  async list(params: { search?: string; storeId?: string; status?: string }) {
    const filter: Record<string, unknown> = {};
    if (params.storeId) filter.storeId = params.storeId;
    if (params.status) filter.status = params.status;
    if (params.search) {
      filter.intentNo = { $regex: params.search, $options: 'i' };
    }
    const rows = await this.model.find(filter).sort({ updatedAt: -1 }).limit(200).lean();
    return await attachLineProducts(
      this.productModel,
      rows as Array<{ lines?: Array<Record<string, unknown>> }>,
    );
  }

  private parseDateBound(value: string, endOfDay: boolean): Date {
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) {
      throw new BadRequestException(`Invalid date: ${value}`);
    }
    if (endOfDay && !value.includes('T')) {
      d.setHours(23, 59, 59, 999);
    }
    return d;
  }

  async filter(dto: FilterPurchaseIntentDto) {
    const filter: FilterQuery<PurchaseIntentDocument> = {};

    if (dto.intentNo) filter.intentNo = dto.intentNo;
    if (dto.storeId) filter.storeId = dto.storeId;
    if (dto.deviceId) filter.deviceId = dto.deviceId;
    if (dto.sourceEventId) filter.sourceEventId = dto.sourceEventId;
    if (dto.status) filter.status = dto.status;

    if (dto.createdAtFrom || dto.createdAtTo) {
      filter.createdAt = {};
      if (dto.createdAtFrom) filter.createdAt.$gte = this.parseDateBound(dto.createdAtFrom, false);
      if (dto.createdAtTo) filter.createdAt.$lte = this.parseDateBound(dto.createdAtTo, true);
    }

    if (dto.updatedAtFrom || dto.updatedAtTo) {
      filter.updatedAt = {};
      if (dto.updatedAtFrom) filter.updatedAt.$gte = this.parseDateBound(dto.updatedAtFrom, false);
      if (dto.updatedAtTo) filter.updatedAt.$lte = this.parseDateBound(dto.updatedAtTo, true);
    }

    if (dto.sku) {
      filter['lines.sku'] = { $regex: dto.sku, $options: 'i' };
    }

    if (dto.search) {
      const rx = { $regex: dto.search, $options: 'i' };
      filter.$or = [
        { intentNo: rx },
        { remarks: rx },
        { 'lines.sku': rx },
        { 'lines.barcode': rx },
        { 'lines.description': rx },
      ];
    }

    const page = dto.page ?? 1;
    const limit = dto.limit ?? 20;
    const skip = (page - 1) * limit;
    const sortBy = dto.sortBy ?? 'updatedAt';
    const sortOrder: SortOrder = dto.sortOrder === 'asc' ? 1 : -1;

    const [data, total] = await Promise.all([
      this.model
        .find(filter)
        .populate(['storeId', 'deviceId', 'sourceEventId'])
        .sort({ [sortBy]: sortOrder })
        .skip(skip)
        .limit(limit)
        .lean(),
      this.model.countDocuments(filter),
    ]);

    const enrichedData = await attachLineProducts(
      this.productModel,
      data as Array<{ lines?: Array<Record<string, unknown>> }>,
    );

    return {
      data: enrichedData,
      total,
      page,
      limit,
      totalPages: Math.ceil(total / limit),
    };
  }

  async setStatus(id: string, status: PurchaseIntentStatus) {
    const doc = await this.model.findByIdAndUpdate(id, { $set: { status } }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Purchase intent not found');
    return await enrichDocWithLineProducts(this.productModel, doc as unknown as Record<string, unknown>);
  }
}
