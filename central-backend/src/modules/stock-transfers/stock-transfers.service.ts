import { BadRequestException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model, SortOrder, Types, UpdateQuery } from 'mongoose';
import { attachLineProducts, enrichDocWithLineProducts, resolveProductIdForSku } from '../../common/product-line-enrichment';
import { InventoryService } from '../inventory/inventory.service';
import { Product, ProductDocument } from '../products/schemas/product.schema';
import { PurchaseIntentLine } from '../purchase-intents/schemas/purchase-intent.schema';
import { PurchaseIntentsService } from '../purchase-intents/purchase-intents.service';
import { StoresService } from '../stores/stores.service';
import { LocationsService } from '../locations/locations.service';
import { CreateFromPurchaseIntentDto } from './dto/create-from-purchase-intent.dto';
import { CreateStockTransferDto } from './dto/create-stock-transfer.dto';
import { FilterStockTransferDto } from './dto/filter-stock-transfer.dto';
import { ReceiveStockTransferDto } from './dto/receive-stock-transfer.dto';
import { StockTransferLineDto } from './dto/stock-transfer-line.dto';
import { UpdateStockTransferDto } from './dto/update-stock-transfer.dto';
import {
  StockTransfer,
  StockTransferDirection,
  StockTransferDocument,
  StockTransferLine,
  StockTransferStatus,
} from './schemas/stock-transfer.schema';

const terminal: StockTransferStatus[] = ['completed', 'cancelled'];

const allowedNext: Record<StockTransferStatus, StockTransferStatus[]> = {
  draft: ['in_transit', 'cancelled'],
  in_transit: ['cancelled'],
  awaiting_intake: ['completed'],
  completed: [],
  cancelled: [],
};

@Injectable()
export class StockTransfersService {
  constructor(
    @InjectModel(StockTransfer.name) private readonly model: Model<StockTransferDocument>,
    @InjectModel(Product.name) private readonly productModel: Model<ProductDocument>,
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

  private resolveDirection(value: string | undefined): StockTransferDirection {
    return value === 'store_to_warehouse' ? 'store_to_warehouse' : 'warehouse_to_store';
  }

  private resolveDirectionFromDoc(doc: { direction?: string }): StockTransferDirection {
    return this.resolveDirection(doc.direction);
  }

  private owningStoreId(doc: { direction?: string; toStoreId?: string; fromStoreId?: string }): string {
    return this.resolveDirectionFromDoc(doc) === 'store_to_warehouse'
      ? (doc.fromStoreId ?? '').trim()
      : (doc.toStoreId ?? '').trim();
  }

  /** Mongo filter: transfers relevant to a store (in destination or out source). */
  private storeScopeFilter(storeId: string): Record<string, unknown> {
    const sid = storeId.trim();
    return {
      $or: [
        {
          $or: [{ direction: 'warehouse_to_store' }, { direction: { $exists: false } }],
          toStoreId: sid,
        },
        { direction: 'store_to_warehouse', fromStoreId: sid },
      ],
    };
  }

  /** Resolves DTO `locationId` to an ObjectId after validating active warehouse location. */
  private async resolveWarehouseLocationId(locationId: string | undefined): Promise<Types.ObjectId | undefined> {
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

  private async resolveToLocationId(locationId: string | undefined): Promise<Types.ObjectId | undefined> {
    return this.resolveWarehouseLocationId(locationId);
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

  private async assertSufficientStoreStock(storeId: string, lines: { sku: string; qty: number }[]) {
    const requestedBySku = new Map<string, number>();
    for (const line of lines) {
      const sku = line.sku.trim();
      if (!sku) continue;
      requestedBySku.set(sku, (requestedBySku.get(sku) ?? 0) + line.qty);
    }
    if (requestedBySku.size === 0) return;

    const availableBySku = await this.inventoryService.getStoreQtyBySkus(storeId, [...requestedBySku.keys()]);
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
        `Insufficient store stock. ${shortages.join('; ')}`,
      );
    }
  }

  private async mapTransferLines(lines: StockTransferLineDto[]): Promise<StockTransferLine[]> {
    const out: StockTransferLine[] = [];
    for (const l of lines) {
      const sku = l.sku.trim();
      const productId = await resolveProductIdForSku(this.productModel, sku, l.productId);
      const line: StockTransferLine = { sku, qty: l.qty };
      if (l.description) line.description = l.description;
      if (productId) line.productId = productId;
      out.push(line);
    }
    return out;
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
    const direction = this.resolveDirection(dto.direction);
    const lines = await this.mapTransferLines(dto.lines);
    const transferNo = await this.nextTransferNo();

    if (direction === 'store_to_warehouse') {
      const fromStoreId = dto.fromStoreId?.trim();
      if (!fromStoreId) throw new BadRequestException('fromStoreId is required for store_to_warehouse transfers');
      const storeExists = await this.storesService.existsByCode(fromStoreId);
      if (!storeExists) throw new BadRequestException(`Unknown fromStoreId '${fromStoreId}'`);
      const toLocationId = await this.resolveToLocationId(dto.toLocationId);
      const created = await this.model.create({
        transferNo,
        direction,
        fromKind: 'store' as const,
        fromStoreId,
        ...(toLocationId ? { toLocationId } : {}),
        status: 'draft',
        transferDate: dto.transferDate,
        remarks: dto.remarks,
        stockClassification: this.normalizeStockClassification(dto.stockClassification),
        lines,
      });
      return await enrichDocWithLineProducts(this.productModel, created.toObject() as unknown as Record<string, unknown>);
    }

    const toStoreId = dto.toStoreId?.trim();
    if (!toStoreId) throw new BadRequestException('toStoreId is required for warehouse_to_store transfers');
    const storeExists = await this.storesService.existsByCode(toStoreId);
    if (!storeExists) throw new BadRequestException(`Unknown toStoreId '${toStoreId}'`);
    const fromLocationId = await this.resolveWarehouseLocationId(dto.locationId);
    await this.assertSufficientWarehouseStock(lines);
    const created = await this.model.create({
      transferNo,
      direction: 'warehouse_to_store' as const,
      fromKind: 'warehouse' as const,
      ...(fromLocationId ? { fromLocationId } : {}),
      toStoreId,
      status: 'draft',
      transferDate: dto.transferDate,
      remarks: dto.remarks,
      stockClassification: this.normalizeStockClassification(dto.stockClassification),
      lines,
    });
    return await enrichDocWithLineProducts(this.productModel, created.toObject() as unknown as Record<string, unknown>);
  }

  async createFromPurchaseIntent(intentId: string, dto: CreateFromPurchaseIntentDto) {
    const intentRaw = await this.purchaseIntentsService.findById(intentId);
    const intent = intentRaw as typeof intentRaw & {
      status: string;
      remarks?: string;
      storeId: string;
      lines: PurchaseIntentLine[];
    };
    if (intent.status === 'rejected' || intent.status === 'cancelled') {
      throw new BadRequestException(`Cannot create transfer from intent in status '${intent.status}'`);
    }
    if (intent.status === 'fulfilled') {
      throw new BadRequestException('Cannot create transfer from a fulfilled intent');
    }
    const intentLines = intent.lines ?? [];
    if (!intentLines.length) {
      throw new BadRequestException('Purchase intent has no lines');
    }

    const overrideMap = new Map(
      (dto.lineOverrides ?? []).map((o) => [o.sku.trim(), o.qty] as const),
    );
    const intentSkus = new Set(intentLines.map((l) => l.sku.trim()));
    for (const sku of overrideMap.keys()) {
      if (!intentSkus.has(sku)) {
        throw new BadRequestException(`lineOverrides contains unknown sku: ${sku}`);
      }
    }

    const lines: StockTransferLine[] = [];
    for (const il of intentLines) {
      const sku = il.sku.trim();
      const qtyOverride = overrideMap.get(sku);
      const qty = qtyOverride ?? il.requestedQty;
      if (typeof qty !== 'number' || !Number.isFinite(qty) || qty <= 0) {
        throw new BadRequestException(`Invalid qty for sku ${sku}`);
      }
      const productId = await resolveProductIdForSku(
        this.productModel,
        sku,
        il.productId ? String(il.productId) : undefined,
      );
      const line: StockTransferLine = { sku, qty };
      if (il.description) line.description = il.description;
      if (productId) line.productId = productId;
      lines.push(line);
    }

    await this.assertSufficientWarehouseStock(lines);
    const transferNo = await this.nextTransferNo();
    const fromLocationId = await this.resolveWarehouseLocationId(dto.locationId);
    const created = await this.model.create({
      transferNo,
      direction: 'warehouse_to_store' as const,
      fromKind: 'warehouse' as const,
      ...(fromLocationId ? { fromLocationId } : {}),
      toStoreId: intent.storeId,
      purchaseIntentId: new Types.ObjectId(intentId),
      status: 'draft',
      remarks: intent.remarks,
      stockClassification: this.normalizeStockClassification(dto.stockClassification),
      lines,
    });
    return await enrichDocWithLineProducts(this.productModel, created.toObject() as unknown as Record<string, unknown>);
  }

  async findById(id: string) {
    if (!Types.ObjectId.isValid(id)) throw new NotFoundException('Stock transfer not found');
    const doc = await this.model.findById(id).lean();
    if (!doc) throw new NotFoundException('Stock transfer not found');
    return await enrichDocWithLineProducts(this.productModel, doc as unknown as Record<string, unknown>);
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
        set.fromLocationId = await this.resolveWarehouseLocationId(dto.locationId);
      }
    }
    if (dto.stockClassification !== undefined) {
      set.stockClassification = this.normalizeStockClassification(dto.stockClassification);
    }
    if (dto.lines !== undefined) {
      if (!dto.lines.length) throw new BadRequestException('lines must contain at least one item');
      const lines = await this.mapTransferLines(dto.lines);
      if (this.resolveDirectionFromDoc(current) === 'store_to_warehouse') {
        const fromStoreId = current.fromStoreId?.trim();
        if (!fromStoreId) throw new BadRequestException('Transfer out is missing fromStoreId');
        await this.assertSufficientStoreStock(fromStoreId, lines);
      } else {
        await this.assertSufficientWarehouseStock(lines);
      }
      set.lines = lines;
    }
    const updateOps: UpdateQuery<StockTransferDocument> = {};
    if (Object.keys(set).length) updateOps.$set = set;
    if (Object.keys(unset).length) updateOps.$unset = unset;
    const doc = await this.model.findByIdAndUpdate(id, Object.keys(updateOps).length ? updateOps : { $set: set }, {
      new: true,
    }).lean();
    if (!doc) throw new NotFoundException('Stock transfer not found');
    return await enrichDocWithLineProducts(this.productModel, doc as unknown as Record<string, unknown>);
  }

  async list(params: {
    search?: string;
    toStoreId?: string;
    fromStoreId?: string;
    direction?: string;
    status?: string;
    purchaseIntentId?: string;
  }) {
    const filter: Record<string, unknown> = {};
    const direction = params.direction?.trim();
    if (direction === 'warehouse_to_store' || direction === 'store_to_warehouse') {
      filter.direction = direction;
    }
    if (params.toStoreId) filter.toStoreId = params.toStoreId;
    if (params.fromStoreId) filter.fromStoreId = params.fromStoreId;
    if (params.status) filter.status = params.status;
    if (params.purchaseIntentId && Types.ObjectId.isValid(params.purchaseIntentId)) {
      filter.purchaseIntentId = new Types.ObjectId(params.purchaseIntentId);
    }
    if (params.search) {
      filter.transferNo = { $regex: params.search, $options: 'i' };
    }
    const rows = await this.model.find(filter).sort({ updatedAt: -1 }).limit(200).lean();
    return await attachLineProducts(this.productModel, rows as Array<{ lines?: Array<Record<string, unknown>> }>);
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

  async listAwaitingIntakeForStore(storeId: string, limit: number) {
    const cap = Math.max(1, Math.min(200, limit));
    return await this.model
      .find({ ...this.storeScopeFilter(storeId), status: 'awaiting_intake' })
      .sort({ updatedAt: 1, _id: 1 })
      .limit(cap)
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
          ...this.storeScopeFilter(storeId),
          status: 'completed',
          updatedAt: { $gte: since },
        })
        .sort({ _id: 1 })
        .limit(cap)
        .lean();
    }

    return await this.model
      .find({
        ...this.storeScopeFilter(storeId),
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
    if (this.owningStoreId(doc) !== storeId) {
      throw new BadRequestException(`Transfer '${doc.transferNo}' is not assigned to store '${storeId}'`);
    }

    this.assertReceiptLinesMatch(doc.lines ?? [], payload.lines);

    if (doc.status === 'completed') {
      return await enrichDocWithLineProducts(this.productModel, doc.toObject() as unknown as Record<string, unknown>);
    }
    if (doc.status !== 'awaiting_intake') {
      throw new BadRequestException(`Cannot receive transfer '${doc.transferNo}' from status '${doc.status}'`);
    }

    const updated = await this.transitionStatus(doc, 'completed');
    return await enrichDocWithLineProducts(this.productModel, updated.toObject() as unknown as Record<string, unknown>);
  }

  /** Store confirms physical receipt; moves in_transit → awaiting_intake. */
  async receiveAtStore(id: string, dto: ReceiveStockTransferDto) {
    if (!Types.ObjectId.isValid(id)) throw new NotFoundException('Stock transfer not found');
    const doc = await this.model.findById(id);
    if (!doc) throw new NotFoundException('Stock transfer not found');

    const storeId = dto.storeId.trim();
    const storeExists = await this.storesService.existsByCode(storeId);
    if (!storeExists) throw new BadRequestException(`Unknown storeId '${storeId}'`);
    if (this.owningStoreId(doc) !== storeId) {
      throw new BadRequestException(
        `Transfer '${doc.transferNo}' is not assigned to store '${storeId}'`,
      );
    }

    this.assertReceiptLinesMatch(doc.lines ?? [], dto.lines);

    if (doc.status === 'completed') {
      return await enrichDocWithLineProducts(this.productModel, doc.toObject() as unknown as Record<string, unknown>);
    }
    if (doc.status === 'awaiting_intake') {
      return await enrichDocWithLineProducts(this.productModel, doc.toObject() as unknown as Record<string, unknown>);
    }
    if (doc.status !== 'in_transit') {
      throw new BadRequestException(
        `Cannot receive transfer '${doc.transferNo}' from status '${doc.status}'`,
      );
    }

    doc.receivedAt = dto.receivedAt?.trim() || new Date().toISOString();
    const receivedBy = dto.receivedBy?.trim();
    if (receivedBy) doc.receivedBy = receivedBy;

    await this.transitionStatus(doc, 'awaiting_intake', { allowStoreIntake: true });
    return await enrichDocWithLineProducts(this.productModel, doc.toObject() as unknown as Record<string, unknown>);
  }

  async setStatus(id: string, status: StockTransferStatus) {
    const doc = await this.model.findById(id);
    if (!doc) throw new NotFoundException('Stock transfer not found');
    await this.transitionStatus(doc, status);
    return await enrichDocWithLineProducts(this.productModel, doc.toObject() as unknown as Record<string, unknown>);
  }

  private async transitionStatus(
    doc: StockTransferDocument,
    to: StockTransferStatus,
    options?: { allowStoreIntake?: boolean },
  ) {
    const from = doc.status;
    if (from === to) return doc;

    if (options?.allowStoreIntake && from === 'in_transit' && to === 'awaiting_intake') {
      // Store receive endpoint only; not allowed via POST /:id/status.
    } else {
      this.assertTransition(from, to);
    }

    await this.applyTransferLedger(from, to, doc);
    doc.status = to;
    await doc.save();
    return doc;
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
    const direction = this.resolveDirectionFromDoc(doc);
    const isOut = direction === 'store_to_warehouse';
    const storeId = this.owningStoreId(doc);

    if (from === 'draft' && to === 'in_transit') {
      if (isOut) {
        await this.assertSufficientStoreStock(storeId, lines);
        await this.inventoryService.addLedgerEntries([
          ...lines.map((l) => ({
            sku: l.sku,
            qtyDelta: -l.qty,
            sourceType: 'StockTransferDispatched',
            sourceId: transferId,
            note,
            locationKind: 'store' as const,
            storeId,
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
      } else {
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
      }
      return;
    }

    if (from === 'awaiting_intake' && to === 'completed') {
      if (isOut) {
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
            locationKind: 'warehouse' as const,
          })),
        ]);
      } else {
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
            storeId,
          })),
        ]);
      }
      return;
    }

    if (to === 'cancelled' && (from === 'in_transit' || from === 'awaiting_intake')) {
      if (isOut) {
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
            locationKind: 'store' as const,
            storeId,
          })),
        ]);
      } else {
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
  }

  /** Payload shape for store sync pull (`payload.transfer`). */
  toSyncTransferPayload(transfer: Record<string, unknown> & { _id?: unknown }) {
    const direction = this.resolveDirectionFromDoc(transfer as { direction?: string });
    const row = transfer as {
      _id?: unknown;
      transferNo: string;
      toStoreId?: string;
      fromStoreId?: string;
      status: string;
      transferDate?: string;
      remarks?: string;
      stockClassification?: string;
      fromLocationId?: unknown;
      toLocationId?: unknown;
      lines?: StockTransferLine[];
    };
    return {
      transferId: row._id != null ? String(row._id) : '',
      transferNo: row.transferNo,
      direction,
      toStoreId: row.toStoreId,
      ...(row.fromStoreId ? { fromStoreId: row.fromStoreId } : {}),
      status: row.status,
      transferDate: row.transferDate,
      remarks: row.remarks,
      stockClassification: row.stockClassification ?? 'Normal Stock',
      ...(row.fromLocationId != null ? { fromLocationId: String(row.fromLocationId) } : {}),
      ...(row.toLocationId != null ? { toLocationId: String(row.toLocationId) } : {}),
      lines: row.lines,
    };
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
