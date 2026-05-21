import { BadRequestException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model, SortOrder, Types, UpdateQuery } from 'mongoose';
import { InventoryService } from '../inventory/inventory.service';
import { PurchaseIntentsService } from '../purchase-intents/purchase-intents.service';
import { StoresService } from '../stores/stores.service';
import { LocationsService } from '../locations/locations.service';
import { CreateFromPurchaseIntentDto } from './dto/create-from-purchase-intent.dto';
import { CreateStockTransferDto } from './dto/create-stock-transfer.dto';
import { FilterStockTransferDto } from './dto/filter-stock-transfer.dto';
import { UpdateStockTransferDto } from './dto/update-stock-transfer.dto';
import {
  StockTransfer,
  StockTransferDocument,
  StockTransferLine,
  StockTransferStatus,
} from './schemas/stock-transfer.schema';

const terminal: StockTransferStatus[] = ['completed', 'cancelled'];

const allowedNext: Record<StockTransferStatus, StockTransferStatus[]> = {
  draft: ['in_transit', 'cancelled'],
  in_transit: ['awaiting_intake', 'cancelled'],
  awaiting_intake: ['completed'],
  completed: [],
  cancelled: [],
};

@Injectable()
export class StockTransfersService {
  constructor(
    @InjectModel(StockTransfer.name) private readonly model: Model<StockTransferDocument>,
    private readonly purchaseIntentsService: PurchaseIntentsService,
    private readonly inventoryService: InventoryService,
    private readonly storesService: StoresService,
    private readonly locationsService: LocationsService,
  ) {}

  private async nextTransferNo() {
    const suffix = Math.floor(1000 + Math.random() * 9000);
    return `TR-${suffix}`;
  }

  private normalizeStockClassification(value: string | undefined): string {
    const t = value?.trim();
    if (!t) return 'Normal Stock';
    return t.length > 80 ? t.slice(0, 80) : t;
  }

  /** Resolves DTO `locationId` to an ObjectId after validating active warehouse location. */
  private async resolveFromLocationId(locationId: string | undefined): Promise<Types.ObjectId | undefined> {
    if (locationId === undefined || String(locationId).trim() === '') return undefined;
    const id = String(locationId).trim();
    if (!Types.ObjectId.isValid(id)) {
      throw new BadRequestException('locationId must be a valid Mongo ObjectId');
    }
    const loc = await this.locationsService.findById(id);
    const type = (loc.type ?? '').toString().trim().toLowerCase();
    if (type !== 'warehouse') {
      throw new BadRequestException('locationId must reference a location with type warehouse');
    }
    if (loc.isActive !== true) {
      throw new BadRequestException('locationId must reference an active location');
    }
    return new Types.ObjectId(id);
  }

  private async assertSufficientWarehouseStock(lines: { sku: string; qty: number }[]) {
    const requestedBySku = new Map<string, number>();
    for (const line of lines) {
      const sku = line.sku.trim();
      if (!sku) continue;
      requestedBySku.set(sku, (requestedBySku.get(sku) ?? 0) + line.qty);
    }
    if (requestedBySku.size === 0) return;

    const availableBySku = await this.inventoryService.getWarehouseQtyBySkus([...requestedBySku.keys()]);
    const shortages: string[] = [];
    for (const [sku, requested] of requestedBySku) {
      const available = availableBySku.get(sku) ?? 0;
      if (available < requested) {
        shortages.push(
          `SKU '${sku}': requested ${requested}, available ${available}`,
        );
      }
    }
    if (shortages.length > 0) {
      throw new BadRequestException(
        `Insufficient warehouse stock. ${shortages.join('; ')}`,
      );
    }
  }

  private assertTransition(from: StockTransferStatus, to: StockTransferStatus) {
    if (from === to) return;
    if (terminal.includes(from)) {
      throw new BadRequestException(`Cannot change status from terminal state '${from}'`);
    }
    const ok = allowedNext[from]?.includes(to);
    if (!ok) {
      throw new BadRequestException(`Invalid status transition: ${from} → ${to}`);
    }
  }

  async create(dto: CreateStockTransferDto) {
    if (!dto.lines?.length) {
      throw new BadRequestException('lines must contain at least one item');
    }
    const storeExists = await this.storesService.existsByCode(dto.toStoreId);
    if (!storeExists) throw new BadRequestException(`Unknown toStoreId '${dto.toStoreId}'`);
    const fromLocationId = await this.resolveFromLocationId(dto.locationId);
    const lines = dto.lines.map((l) => ({ sku: l.sku.trim(), description: l.description, qty: l.qty }));
    await this.assertSufficientWarehouseStock(lines);
    const transferNo = await this.nextTransferNo();
    return await this.model.create({
      transferNo,
      fromKind: 'warehouse' as const,
      ...(fromLocationId ? { fromLocationId } : {}),
      toStoreId: dto.toStoreId,
      status: 'draft',
      transferDate: dto.transferDate,
      remarks: dto.remarks,
      stockClassification: this.normalizeStockClassification(dto.stockClassification),
      lines,
    });
  }

  async createFromPurchaseIntent(intentId: string, dto: CreateFromPurchaseIntentDto) {
    const intent = await this.purchaseIntentsService.findById(intentId);
    if (intent.status === 'rejected' || intent.status === 'cancelled') {
      throw new BadRequestException(`Cannot create transfer from intent in status '${intent.status}'`);
    }
    if (intent.status === 'fulfilled') {
      throw new BadRequestException('Cannot create transfer from a fulfilled intent');
    }
    if (!intent.lines?.length) {
      throw new BadRequestException('Purchase intent has no lines');
    }

    const overrideMap = new Map(
      (dto.lineOverrides ?? []).map((o) => [o.sku.trim(), o.qty] as const),
    );
    const intentSkus = new Set(intent.lines.map((l) => l.sku.trim()));
    for (const sku of overrideMap.keys()) {
      if (!intentSkus.has(sku)) {
        throw new BadRequestException(`lineOverrides contains unknown sku: ${sku}`);
      }
    }

    const lines: StockTransferLine[] = intent.lines.map((il) => {
      const sku = il.sku.trim();
      const qtyOverride = overrideMap.get(sku);
      const qty = qtyOverride ?? il.requestedQty;
      if (typeof qty !== 'number' || !Number.isFinite(qty) || qty <= 0) {
        throw new BadRequestException(`Invalid qty for sku ${sku}`);
      }
      const line: StockTransferLine = { sku, qty };
      if (il.description) line.description = il.description;
      return line;
    });

    await this.assertSufficientWarehouseStock(lines);
    const transferNo = await this.nextTransferNo();
    const fromLocationId = await this.resolveFromLocationId(dto.locationId);
    return await this.model.create({
      transferNo,
      fromKind: 'warehouse' as const,
      ...(fromLocationId ? { fromLocationId } : {}),
      toStoreId: intent.storeId,
      purchaseIntentId: new Types.ObjectId(intentId),
      status: 'draft',
      remarks: intent.remarks,
      stockClassification: this.normalizeStockClassification(dto.stockClassification),
      lines,
    });
  }

  async findById(id: string) {
    if (!Types.ObjectId.isValid(id)) throw new NotFoundException('Stock transfer not found');
    const doc = await this.model.findById(id).lean();
    if (!doc) throw new NotFoundException('Stock transfer not found');
    return doc;
  }

  async update(id: string, dto: UpdateStockTransferDto) {
    const current = await this.model.findById(id);
    if (!current) throw new NotFoundException('Stock transfer not found');
    if (current.status !== 'draft') {
      throw new BadRequestException('Only draft transfers can be updated');
    }
    const set: Record<string, unknown> = {};
    const unset: Record<string, 1> = {};
    if (dto.transferDate !== undefined) set.transferDate = dto.transferDate;
    if (dto.remarks !== undefined) set.remarks = dto.remarks;
    if (dto.locationId !== undefined) {
      const raw = String(dto.locationId).trim();
      if (!raw) {
        unset.fromLocationId = 1;
      } else {
        set.fromLocationId = await this.resolveFromLocationId(dto.locationId);
      }
    }
    if (dto.stockClassification !== undefined) {
      set.stockClassification = this.normalizeStockClassification(dto.stockClassification);
    }
    if (dto.lines !== undefined) {
      if (!dto.lines.length) throw new BadRequestException('lines must contain at least one item');
      const lines = dto.lines.map((l) => ({ sku: l.sku.trim(), description: l.description, qty: l.qty }));
      await this.assertSufficientWarehouseStock(lines);
      set.lines = lines;
    }
    const updateOps: UpdateQuery<StockTransferDocument> = {};
    if (Object.keys(set).length) updateOps.$set = set;
    if (Object.keys(unset).length) updateOps.$unset = unset;
    const doc = await this.model.findByIdAndUpdate(id, Object.keys(updateOps).length ? updateOps : { $set: set }, {
      new: true,
    }).lean();
    if (!doc) throw new NotFoundException('Stock transfer not found');
    return doc;
  }

  async list(params: {
    search?: string;
    toStoreId?: string;
    status?: string;
    purchaseIntentId?: string;
  }) {
    const filter: Record<string, unknown> = {};
    if (params.toStoreId) filter.toStoreId = params.toStoreId;
    if (params.status) filter.status = params.status;
    if (params.purchaseIntentId && Types.ObjectId.isValid(params.purchaseIntentId)) {
      filter.purchaseIntentId = new Types.ObjectId(params.purchaseIntentId);
    }
    if (params.search) {
      filter.transferNo = { $regex: params.search, $options: 'i' };
    }
    return await this.model.find(filter).sort({ updatedAt: -1 }).limit(200).lean();
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

  async filter(dto: FilterStockTransferDto) {
    const filter: FilterQuery<StockTransferDocument> = {};

    if (dto.transferNo) filter.transferNo = dto.transferNo;
    if (dto.toStoreId) filter.toStoreId = dto.toStoreId;
    if (dto.status) filter.status = dto.status;
    if (dto.stockClassification) filter.stockClassification = dto.stockClassification;

    if (dto.fromLocationId) {
      if (!Types.ObjectId.isValid(dto.fromLocationId)) {
        throw new BadRequestException('fromLocationId must be a valid Mongo ObjectId');
      }
      filter.fromLocationId = new Types.ObjectId(dto.fromLocationId);
    }

    if (dto.purchaseIntentId) {
      if (!Types.ObjectId.isValid(dto.purchaseIntentId)) {
        throw new BadRequestException('purchaseIntentId must be a valid Mongo ObjectId');
      }
      filter.purchaseIntentId = new Types.ObjectId(dto.purchaseIntentId);
    }

    if (dto.transferDateFrom || dto.transferDateTo) {
      filter.transferDate = {};
      if (dto.transferDateFrom) filter.transferDate.$gte = dto.transferDateFrom;
      if (dto.transferDateTo) filter.transferDate.$lte = dto.transferDateTo;
    }

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
        { transferNo: rx },
        { remarks: rx },
        { 'lines.sku': rx },
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
        .populate(['fromLocationId', 'toStoreId', 'purchaseIntentId'])
        .sort({ [sortBy]: sortOrder })
        .skip(skip)
        .limit(limit)
        .lean(),
      this.model.countDocuments(filter),
    ]);

    return {
      data,
      total,
      page,
      limit,
      totalPages: Math.ceil(total / limit),
    };
  }

  async listAwaitingIntakeForStore(storeId: string, limit: number) {
    return await this.model
      .find({ toStoreId: storeId, status: 'awaiting_intake' })
      .sort({ updatedAt: 1, _id: 1 })
      .limit(limit)
      .lean();
  }

  /**
   * Completed transfers for store sync pull (other POS devices that missed awaiting_intake).
   * - Bootstrap (invalid / "0" cursor): last 90 days by updatedAt, capped.
   * - Incremental: _id greater than sinceTransferCursor.
   */
  async listCompletedForStorePullWindow(storeId: string, sinceTransferCursor: string, limit: number) {
    const cap = Math.max(1, Math.min(200, limit));
    const validSince =
      sinceTransferCursor &&
      sinceTransferCursor !== '0' &&
      Types.ObjectId.isValid(sinceTransferCursor);

    if (!validSince) {
      const since = new Date(Date.now() - 90 * 86400000);
      return await this.model
        .find({
          toStoreId: storeId,
          status: 'completed',
          updatedAt: { $gte: since },
        })
        .sort({ _id: 1 })
        .limit(cap)
        .lean();
    }

    return await this.model
      .find({
        toStoreId: storeId,
        status: 'completed',
        _id: { $gt: new Types.ObjectId(sinceTransferCursor) },
      })
      .sort({ _id: 1 })
      .limit(cap)
      .lean();
  }

  async receiveFromSync(storeId: string, payload: Record<string, unknown>) {
    const transferId = this.readPayloadString(payload, 'transferId');
    const transferNo = this.readPayloadString(payload, 'transferNo');
    if (!transferId && !transferNo) {
      throw new BadRequestException('transferId or transferNo is required');
    }

    const doc = transferId && Types.ObjectId.isValid(transferId)
      ? await this.model.findById(transferId)
      : await this.model.findOne({ transferNo });

    if (!doc) throw new NotFoundException('Stock transfer not found');
    if (doc.toStoreId !== storeId) {
      throw new BadRequestException(`Transfer '${doc.transferNo}' is not assigned to store '${storeId}'`);
    }

    this.assertReceiptLinesMatch(doc.lines ?? [], payload.lines);

    if (doc.status === 'completed') {
      return doc.toObject();
    }
    if (doc.status !== 'awaiting_intake') {
      throw new BadRequestException(`Cannot receive transfer '${doc.transferNo}' from status '${doc.status}'`);
    }

    return await this.setStatus(String(doc._id), 'completed');
  }

  async setStatus(id: string, status: StockTransferStatus) {
    const doc = await this.model.findById(id);
    if (!doc) throw new NotFoundException('Stock transfer not found');
    const from = doc.status;
    this.assertTransition(from, status);
    if (from !== status) {
      await this.applyTransferLedger(from, status, doc);
    }
    doc.status = status;
    await doc.save();
    return doc.toObject();
  }

  /** Posts warehouse / store ledger movements for transfer lifecycle. */
  private async applyTransferLedger(
    from: StockTransferStatus,
    to: StockTransferStatus,
    doc: StockTransferDocument,
  ) {
    const transferId = String(doc._id);
    const lines = doc.lines ?? [];
    const note = doc.transferNo;

    if (from === 'draft' && to === 'in_transit') {
      await this.assertSufficientWarehouseStock(lines);
      await this.inventoryService.addLedgerEntries([
        ...lines.map((l) => ({
          sku: l.sku,
          qtyDelta: -l.qty,
          sourceType: 'StockTransferDispatched',
          sourceId: transferId,
          note,
          locationKind: 'warehouse' as const,
        })),
        ...lines.map((l) => ({
          sku: l.sku,
          qtyDelta: l.qty,
          sourceType: 'StockTransferDispatched',
          sourceId: transferId,
          note,
          locationKind: 'in_transit' as const,
        })),
      ]);
      return;
    }

    if (from === 'awaiting_intake' && to === 'completed') {
      await this.inventoryService.addLedgerEntries([
        ...lines.map((l) => ({
          sku: l.sku,
          qtyDelta: -l.qty,
          sourceType: 'StockTransferReceived',
          sourceId: transferId,
          note,
          locationKind: 'in_transit' as const,
        })),
        ...lines.map((l) => ({
          sku: l.sku,
          qtyDelta: l.qty,
          sourceType: 'StockTransferReceived',
          sourceId: transferId,
          note,
          locationKind: 'store' as const,
          storeId: doc.toStoreId,
        })),
      ]);
      return;
    }

    if (to === 'cancelled' && (from === 'in_transit' || from === 'awaiting_intake')) {
      await this.inventoryService.addLedgerEntries([
        ...lines.map((l) => ({
          sku: l.sku,
          qtyDelta: -l.qty,
          sourceType: 'StockTransferCancelled',
          sourceId: transferId,
          note,
          locationKind: 'in_transit' as const,
        })),
        ...lines.map((l) => ({
          sku: l.sku,
          qtyDelta: l.qty,
          sourceType: 'StockTransferCancelled',
          sourceId: transferId,
          note,
          locationKind: 'warehouse' as const,
        })),
      ]);
    }
  }

  private readPayloadString(payload: Record<string, unknown>, key: string) {
    const value = payload[key];
    return typeof value === 'string' ? value.trim() : '';
  }

  private assertReceiptLinesMatch(expected: StockTransferLine[], actual: unknown) {
    if (!Array.isArray(actual) || actual.length === 0) {
      throw new BadRequestException('lines must contain at least one item');
    }

    const expectedBySku = new Map<string, number>();
    for (const line of expected) {
      const sku = line.sku.trim();
      expectedBySku.set(sku, (expectedBySku.get(sku) ?? 0) + line.qty);
    }

    const actualBySku = new Map<string, number>();
    for (const raw of actual) {
      if (!raw || typeof raw !== 'object') {
        throw new BadRequestException('Each receipt line must be an object');
      }
      const line = raw as Record<string, unknown>;
      const sku = typeof line.sku === 'string' ? line.sku.trim() : '';
      const qty = typeof line.qty === 'number' ? line.qty : Number(line.qty);
      if (!sku || !Number.isFinite(qty) || qty <= 0) {
        throw new BadRequestException('Each receipt line needs sku and qty > 0');
      }
      actualBySku.set(sku, (actualBySku.get(sku) ?? 0) + qty);
    }

    if (actualBySku.size !== expectedBySku.size) {
      throw new BadRequestException('Receipt lines do not match transfer lines');
    }

    for (const [sku, qty] of expectedBySku) {
      if ((actualBySku.get(sku) ?? 0) !== qty) {
        throw new BadRequestException(`Receipt quantity mismatch for sku ${sku}`);
      }
    }
  }
}
