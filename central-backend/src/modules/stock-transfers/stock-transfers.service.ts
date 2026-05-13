import { BadRequestException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model, Types } from 'mongoose';
import { InventoryService } from '../inventory/inventory.service';
import { PurchaseIntentsService } from '../purchase-intents/purchase-intents.service';
import { StoresService } from '../stores/stores.service';
import { CreateFromPurchaseIntentDto } from './dto/create-from-purchase-intent.dto';
import { CreateStockTransferDto } from './dto/create-stock-transfer.dto';
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
  ) {}

  private async nextTransferNo() {
    const suffix = Math.floor(1000 + Math.random() * 9000);
    return `TR-${suffix}`;
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
    const transferNo = await this.nextTransferNo();
    return await this.model.create({
      transferNo,
      fromKind: 'warehouse' as const,
      toStoreId: dto.toStoreId,
      status: 'draft',
      transferDate: dto.transferDate,
      remarks: dto.remarks,
      lines: dto.lines.map((l) => ({ sku: l.sku.trim(), description: l.description, qty: l.qty })),
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

    const transferNo = await this.nextTransferNo();
    return await this.model.create({
      transferNo,
      fromKind: 'warehouse' as const,
      toStoreId: intent.storeId,
      purchaseIntentId: new Types.ObjectId(intentId),
      status: 'draft',
      remarks: intent.remarks,
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
    if (dto.transferDate !== undefined) set.transferDate = dto.transferDate;
    if (dto.remarks !== undefined) set.remarks = dto.remarks;
    if (dto.lines !== undefined) {
      if (!dto.lines.length) throw new BadRequestException('lines must contain at least one item');
      set.lines = dto.lines.map((l) => ({ sku: l.sku.trim(), description: l.description, qty: l.qty }));
    }
    const doc = await this.model.findByIdAndUpdate(id, { $set: set }, { new: true }).lean();
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
}
