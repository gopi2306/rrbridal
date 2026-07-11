import {
  BadRequestException,
  ConflictException,
  Injectable,
  NotFoundException,
} from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model, Types } from 'mongoose';
import { DocumentNumberService } from '../../common/document-number.service';
import { DocumentNumberAllocatorService } from '../document-numbers/document-number-allocator.service';
import { DocumentNumberConfigService } from '../document-numbers/document-number-config.service';
import { InventoryService } from '../inventory/inventory.service';
import { Location, LocationDocument } from '../locations/schemas/location.schema';
import { ProductsService } from '../products/products.service';
import { Store, StoreDocument } from '../stores/schemas/store.schema';
import { CreateInventoryAdjustmentDto } from './dto/create-inventory-adjustment.dto';
import { INVENTORY_ADJUSTMENT_POSTED } from './inventory-adjustments.constants';
import {
  InventoryAdjustment,
  InventoryAdjustmentDocument,
  InventoryAdjustmentLine,
  InventoryAdjustmentLocationKind,
  InventoryAdjustmentSource,
} from './schemas/inventory-adjustment.schema';

export type SyncEventMeta = {
  eventId: string;
  storeId: string;
  deviceId: string;
};

type LineInput = {
  sku: string;
  qtyDelta?: number;
  newQty?: number;
  note?: string;
};

type ResolvedLine = InventoryAdjustmentLine;

@Injectable()
export class InventoryAdjustmentsService {
  constructor(
    private readonly inventoryService: InventoryService,
    private readonly productsService: ProductsService,
    private readonly allocator: DocumentNumberAllocatorService,
    private readonly configService: DocumentNumberConfigService,
    @InjectModel(InventoryAdjustment.name)
    private readonly adjustmentModel: Model<InventoryAdjustmentDocument>,
    @InjectModel(Store.name) private readonly storeModel: Model<StoreDocument>,
    @InjectModel(Location.name) private readonly locationModel: Model<LocationDocument>,
  ) {}

  async createFromAdmin(dto: CreateInventoryAdjustmentDto) {
    const { locationKind, storeId, locationCode } = await this.resolveLocation(dto);
    const lines = await this.resolveLines(locationKind, storeId, locationCode, dto.lines);
    const adjustmentNo = await this.allocateAdjustmentNo();

    const doc = await this.persistAndPostLedger({
      adjustmentNo,
      locationKind,
      ...(storeId ? { storeId } : {}),
      ...(locationCode ? { locationCode } : {}),
      source: 'central_admin',
      reason: dto.reason.trim(),
      lines,
    });

    return this.toResponse(doc as unknown as Record<string, unknown>);
  }

  async applyFromSync(meta: SyncEventMeta, payload: Record<string, unknown>) {
    const existing = await this.adjustmentModel.findOne({ sourceEventId: meta.eventId }).lean();
    if (existing) return existing;

    const locationKind = this.readPayloadLocationKind(payload);
    if (locationKind !== 'store') {
      throw new BadRequestException('WPF sync adjustments must use locationKind store');
    }

    const reason = this.requireString(payload, 'reason');
    const rawLines = this.readPayloadLines(payload);
    const storeId = await this.resolveStoreCode(meta.storeId);
    const lines = await this.resolveLines(locationKind, storeId, undefined, rawLines);
    const adjustmentNo = await this.allocateAdjustmentNo();

    try {
      const doc = await this.persistAndPostLedger({
        adjustmentNo,
        locationKind,
        storeId,
        source: 'wpf_sync',
        sourceEventId: meta.eventId,
        deviceId: meta.deviceId,
        reason,
        lines,
      });
      return doc;
    } catch (err) {
      if (err instanceof ConflictException) {
        const again = await this.adjustmentModel.findOne({ sourceEventId: meta.eventId }).lean();
        if (again) return again;
      }
      throw err;
    }
  }

  async findById(id: string) {
    if (!Types.ObjectId.isValid(id)) throw new NotFoundException('Inventory adjustment not found');
    const doc = await this.adjustmentModel.findById(id).lean();
    if (!doc) throw new NotFoundException('Inventory adjustment not found');
    return this.toResponse(doc as unknown as Record<string, unknown>);
  }

  async list(params: {
    storeCode?: string;
    locationCode?: string;
    locationKind?: InventoryAdjustmentLocationKind;
    search?: string;
    page?: number;
    limit?: number;
  }) {
    const page = Math.max(1, params.page ?? 1);
    const limit = Math.min(100, Math.max(1, params.limit ?? 20));
    const filter: FilterQuery<InventoryAdjustmentDocument> = { status: 'posted' };

    if (params.locationKind) filter.locationKind = params.locationKind;
    if (params.storeCode?.trim()) {
      filter.storeId = params.storeCode.trim().toLowerCase();
    }
    if (params.locationCode?.trim()) {
      filter.locationCode = params.locationCode.trim().toLowerCase();
    }

    const trimmedSearch = params.search?.trim();
    if (trimmedSearch) {
      const regex = new RegExp(trimmedSearch.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'i');
      filter.$or = [
        { adjustmentNo: regex },
        { reason: regex },
        { 'lines.sku': regex },
      ];
    }

    const [docs, total] = await Promise.all([
      this.adjustmentModel
        .find(filter)
        .sort({ createdAt: -1, _id: -1 })
        .skip((page - 1) * limit)
        .limit(limit)
        .lean(),
      this.adjustmentModel.countDocuments(filter),
    ]);

    return {
      data: docs.map((doc) => this.toResponse(doc as unknown as Record<string, unknown>)),
      total,
      page,
      limit,
      totalPages: total > 0 ? Math.ceil(total / limit) : 0,
    };
  }

  async listForStorePull(storeId: string, sinceAdjustmentCursor: string, limit: number) {
    const sid = storeId.trim().toLowerCase();
    const cap = Math.max(1, Math.min(200, limit));
    const validSince =
      sinceAdjustmentCursor &&
      sinceAdjustmentCursor !== '0' &&
      Types.ObjectId.isValid(sinceAdjustmentCursor);

    const baseFilter: FilterQuery<InventoryAdjustmentDocument> = {
      locationKind: 'store',
      storeId: sid,
      status: 'posted',
    };

    if (!validSince) {
      const since = new Date(Date.now() - 90 * 86400000);
      return await this.adjustmentModel
        .find({ ...baseFilter, createdAt: { $gte: since } })
        .sort({ _id: 1 })
        .limit(cap)
        .lean();
    }

    return await this.adjustmentModel
      .find({
        ...baseFilter,
        _id: { $gt: new Types.ObjectId(sinceAdjustmentCursor) },
      })
      .sort({ _id: 1 })
      .limit(cap)
      .lean();
  }

  toSyncPayload(doc: Record<string, unknown>) {
    const lines = Array.isArray(doc.lines) ? doc.lines : [];
    return {
      adjustmentId: doc._id ? String(doc._id) : '',
      adjustmentNo: typeof doc.adjustmentNo === 'string' ? doc.adjustmentNo : '',
      sourceEventId: typeof doc.sourceEventId === 'string' ? doc.sourceEventId : undefined,
      reason: typeof doc.reason === 'string' ? doc.reason : '',
      lines: lines.map((line) => {
        const row = line as Record<string, unknown>;
        return {
          sku: typeof row.sku === 'string' ? row.sku : '',
          qtyDelta: typeof row.qtyDelta === 'number' ? row.qtyDelta : 0,
          note: typeof row.note === 'string' ? row.note : undefined,
        };
      }),
    };
  }

  private async persistAndPostLedger(input: {
    adjustmentNo: string;
    locationKind: InventoryAdjustmentLocationKind;
    storeId?: string;
    locationCode?: string;
    source: InventoryAdjustmentSource;
    sourceEventId?: string;
    deviceId?: string;
    reason: string;
    lines: ResolvedLine[];
  }) {
    const doc = await this.adjustmentModel.create({
      adjustmentNo: input.adjustmentNo,
      locationKind: input.locationKind,
      storeId: input.storeId,
      locationCode: input.locationCode,
      source: input.source,
      sourceEventId: input.sourceEventId,
      deviceId: input.deviceId,
      reason: input.reason,
      status: 'posted',
      lines: input.lines,
    });

    const sourceId = String(doc._id);
    const note = `${input.adjustmentNo}: ${input.reason}`;

    await this.inventoryService.addLedgerEntries(
      input.lines.map((line) => {
        const entry: {
          sku: string;
          qtyDelta: number;
          sourceType: string;
          sourceId: string;
          note: string;
          locationKind: InventoryAdjustmentLocationKind;
          storeId?: string;
          locationCode?: string;
        } = {
          sku: line.sku,
          qtyDelta: line.qtyDelta,
          sourceType: INVENTORY_ADJUSTMENT_POSTED,
          sourceId,
          note: line.note ? `${note} (${line.note})` : note,
          locationKind: input.locationKind,
        };
        if (input.locationKind === 'store' && input.storeId) entry.storeId = input.storeId;
        if (input.locationKind === 'warehouse' && input.locationCode) entry.locationCode = input.locationCode;
        return entry;
      }),
    );

    return doc;
  }

  private async resolveLocation(dto: CreateInventoryAdjustmentDto) {
    if (dto.locationKind === 'store') {
      const storeId = await this.resolveStoreCode(dto.storeCode ?? '');
      return { locationKind: dto.locationKind, storeId, locationCode: undefined };
    }

    const locationCode = dto.locationCode?.trim().toLowerCase() ?? '';
    if (!locationCode) {
      throw new BadRequestException('locationCode is required for warehouse adjustments');
    }

    const location = await this.locationModel
      .findOne({ code: locationCode, isActive: true })
      .lean();
    if (!location) {
      throw new NotFoundException(`Warehouse location '${dto.locationCode}' not found or inactive`);
    }

    return { locationKind: dto.locationKind, storeId: undefined, locationCode };
  }

  private async resolveStoreCode(storeCode: string): Promise<string> {
    const code = storeCode.trim().toLowerCase();
    const store = await this.storeModel.findOne({ code, status: 'active' }).lean();
    if (!store) {
      throw new NotFoundException(`Store '${storeCode}' not found or inactive`);
    }
    return store.code;
  }

  private async resolveLines(
    locationKind: InventoryAdjustmentLocationKind,
    storeId: string | undefined,
    locationCode: string | undefined,
    inputs: LineInput[],
  ): Promise<ResolvedLine[]> {
    if (inputs.length === 0) {
      throw new BadRequestException('At least one adjustment line is required');
    }

    const bySku = new Map<string, LineInput>();
    for (const input of inputs) {
      const sku = input.sku?.trim() ?? '';
      if (!sku) continue;
      bySku.set(sku, input);
    }

    if (bySku.size === 0) {
      throw new BadRequestException('At least one line with a valid SKU is required');
    }

    const skus = [...bySku.keys()];
    await this.assertSkusExist(skus);

    let qtyBeforeMap: Map<string, number>;
    if (locationKind === 'store') {
      qtyBeforeMap = await this.inventoryService.getStoreQtyBySkus(storeId!, skus);
    } else {
      const warehouseCode = locationCode?.trim().toLowerCase() ?? '';
      if (!warehouseCode) {
        throw new BadRequestException('locationCode is required for warehouse adjustments');
      }
      const legacyCode =
        (await this.inventoryService.getDefaultWarehouseLocationCode()) ?? warehouseCode;
      qtyBeforeMap = await this.inventoryService.getWarehouseQtyBySkusAtLocation(
        warehouseCode,
        skus,
        legacyCode,
      );
    }

    const resolved: ResolvedLine[] = [];
    for (const sku of skus) {
      const input = bySku.get(sku)!;
      const qtyBefore = qtyBeforeMap.get(sku) ?? 0;
      const qtyDelta = this.resolveQtyDelta(input, qtyBefore, sku);
      const qtyAfter = qtyBefore + qtyDelta;

      if (qtyAfter < 0) {
        throw new BadRequestException(
          `Adjustment for SKU '${sku}' would result in negative on-hand (${qtyAfter})`,
        );
      }

      const line: ResolvedLine = { sku, qtyBefore, qtyDelta, qtyAfter };
      if (input.note?.trim()) line.note = input.note.trim();
      resolved.push(line);
    }

    return resolved;
  }

  private resolveQtyDelta(input: LineInput, qtyBefore: number, sku: string): number {
    const hasDelta = input.qtyDelta !== undefined && input.qtyDelta !== null;
    const hasNewQty = input.newQty !== undefined && input.newQty !== null;

    if (!hasDelta && !hasNewQty) {
      throw new BadRequestException(`Line for SKU '${sku}' requires qtyDelta or newQty`);
    }

    const qtyDelta = hasNewQty ? Number(input.newQty) - qtyBefore : Number(input.qtyDelta);
    if (!Number.isFinite(qtyDelta) || qtyDelta === 0) {
      throw new BadRequestException(`Line for SKU '${sku}' must have a non-zero qtyDelta`);
    }

    return qtyDelta;
  }

  private async assertSkusExist(skus: string[]) {
    for (const sku of skus) {
      const product = await this.productsService.findBySku(sku);
      if (!product) {
        throw new NotFoundException(`No product found for SKU '${sku}'`);
      }
    }
  }

  private async allocateAdjustmentNo(): Promise<string> {
    const config = await this.configService.getByKey('inventory_adjustment');
    const prefix = config.prefix;
    return this.allocator.allocate('inventory_adjustment', {
      exists: async (v) => !!(await this.adjustmentModel.exists({ adjustmentNo: v }).lean()),
      syncFloorFromValues: () => this.maxAdjustmentSequenceForPrefix(prefix),
    });
  }

  private async maxAdjustmentSequenceForPrefix(prefix: string): Promise<number> {
    const escaped = prefix.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const regex = new RegExp(`^${escaped}\\d+$`, 'i');
    const rows = await this.adjustmentModel.find({ adjustmentNo: regex }).select('adjustmentNo').lean();
    let max = 0;
    for (const row of rows) {
      if (typeof row.adjustmentNo !== 'string') continue;
      const n = DocumentNumberService.parseSequenceNumber(row.adjustmentNo, prefix);
      if (n !== null && n > max) max = n;
    }
    return max;
  }

  private readPayloadLocationKind(payload: Record<string, unknown>): InventoryAdjustmentLocationKind {
    const value = payload.locationKind;
    if (value === 'store' || value === 'warehouse') return value;
    return 'store';
  }

  private requireString(payload: Record<string, unknown>, key: string): string {
    const value = payload[key];
    if (typeof value !== 'string' || !value.trim()) {
      throw new BadRequestException(`${key} is required`);
    }
    return value.trim();
  }

  private readPayloadLines(payload: Record<string, unknown>): LineInput[] {
    const raw = payload.lines;
    if (!Array.isArray(raw) || raw.length === 0) {
      throw new BadRequestException('lines is required and must contain at least one item');
    }

    const lines: LineInput[] = [];
    for (const item of raw) {
      if (!item || typeof item !== 'object') continue;
      const row = item as Record<string, unknown>;
      const sku = typeof row.sku === 'string' ? row.sku.trim() : '';
      if (!sku) continue;

      const line: LineInput = { sku };
      if (typeof row.note === 'string' && row.note.trim()) line.note = row.note.trim();
      if (typeof row.qtyDelta === 'number' && Number.isFinite(row.qtyDelta)) {
        line.qtyDelta = row.qtyDelta;
      } else if (typeof row.newQty === 'number' && Number.isFinite(row.newQty)) {
        line.newQty = row.newQty;
      }
      lines.push(line);
    }

    if (lines.length === 0) {
      throw new BadRequestException('lines must include at least one valid SKU');
    }

    return lines;
  }

  private toResponse(doc: Record<string, unknown>) {
    return {
      id: doc._id ? String(doc._id) : undefined,
      adjustmentNo: doc.adjustmentNo,
      locationKind: doc.locationKind,
      storeId: doc.storeId,
      locationCode: doc.locationCode,
      source: doc.source,
      sourceEventId: doc.sourceEventId,
      deviceId: doc.deviceId,
      reason: doc.reason,
      status: doc.status,
      lines: doc.lines,
      createdAt: doc.createdAt,
      updatedAt: doc.updatedAt,
    };
  }
}
