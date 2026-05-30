import { Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model, Types } from 'mongoose';
import { Category, CategoryDocument } from '../categories/schemas/category.schema';
import { InventoryLedgerEntry, InventoryLedgerDocument } from '../inventory/schemas/inventory-ledger.schema';
import {
  PurchaseIntent,
  PurchaseIntentDocument,
} from '../purchase-intents/schemas/purchase-intent.schema';
import { Product, ProductDocument } from '../products/schemas/product.schema';
import { StockTransfer, StockTransferDocument } from '../stock-transfers/schemas/stock-transfer.schema';
import { Store, StoreDocument } from '../stores/schemas/store.schema';
import type {
  StoreCategoryMixRow,
  StoreDashboardOptions,
  StoreDashboardResponse,
  StoreLowStockRow,
  StoreNetworkRow,
  StoreRecentActivity,
  StoreTransferScheduleRow,
} from './store-dashboard.types';

type StoreSkuMap = Map<string, number>;
type AllStoresQty = Map<string, StoreSkuMap>;

type ProductLean = {
  sku: string;
  itemName: string;
  categoryId?: Types.ObjectId;
  storePrice?: number;
  sellingPrice?: number;
  minimumShelfFit?: number;
  minStock?: number;
  reorderLevel?: number;
  isActive?: boolean;
};

type LowStockEval = {
  rows: StoreLowStockRow[];
  count: number;
};

type StoreMetricsSnapshot = {
  totalSkus: number;
  onShelfUnits: number;
  retailValue: number;
  lowStockSkus: number;
};

const OPEN_INTENT_STATUSES = ['submitted', 'under_review', 'approved'] as const;
const INBOUND_TRANSFER_STATUSES = ['draft', 'in_transit', 'awaiting_intake'] as const;

@Injectable()
export class StoreDashboardService {
  constructor(
    @InjectModel(InventoryLedgerEntry.name) private readonly ledgerModel: Model<InventoryLedgerDocument>,
    @InjectModel(Product.name) private readonly productModel: Model<ProductDocument>,
    @InjectModel(Category.name) private readonly categoryModel: Model<CategoryDocument>,
    @InjectModel(StockTransfer.name) private readonly stModel: Model<StockTransferDocument>,
    @InjectModel(PurchaseIntent.name) private readonly intentModel: Model<PurchaseIntentDocument>,
    @InjectModel(Store.name) private readonly storeModel: Model<StoreDocument>,
  ) {}

  async getStoreDashboard(options: StoreDashboardOptions): Promise<StoreDashboardResponse> {
    const availableStores = await this.listActiveStores();
    const store = await this.resolveStore(options.storeId, availableStores);

    const [allStoreQty, products, inTransitByStore, openRequests] = await Promise.all([
      this.buildAllStoreQtyMaps(),
      this.loadProductsForDashboard(),
      this.sumInboundTransferPiecesByStore(),
      this.countOpenIntents(store.code),
    ]);

    const storeQty = allStoreQty.get(store.code) ?? new Map<string, number>();
    const lowStockEval = this.evaluateLowStock(storeQty, products, options.lowStockLimit);
    const metricsSnapshot = this.computeStoreMetrics(storeQty, products, lowStockEval.count);

    const storeNetwork = this.buildStoreNetwork(
      availableStores,
      allStoreQty,
      products,
    );

    const [
      categoryMix,
      recentActivity,
      transferSchedule,
    ] = await Promise.all([
      Promise.resolve(this.getCategoryMix(storeQty, products)),
      this.getRecentActivity(store.code, options, lowStockEval.rows, availableStores),
      this.getTransferSchedule(store.code, options.transferLimit, availableStores),
    ]);

    return {
      store,
      availableStores,
      metrics: {
        ...metricsSnapshot,
        inTransitUnits: inTransitByStore.get(store.code) ?? 0,
        openRequests,
      },
      storeNetwork,
      categoryMix,
      recentActivity,
      lowStock: lowStockEval.rows,
      transferSchedule,
    };
  }

  private async listActiveStores() {
    const stores = await this.storeModel.find({ status: 'active' }).sort({ code: 1 }).lean();
    return stores.map((s) => ({ code: s.code, name: s.name }));
  }

  private async resolveStore(
    storeId: string | undefined,
    available: Array<{ code: string; name: string }>,
  ) {
    const code = storeId?.trim().toLowerCase();
    const match = code
      ? available.find((s) => s.code === code)
      : available[0];

    if (!match) {
      if (code) throw new NotFoundException(`Store '${code}' not found or inactive`);
      throw new NotFoundException('No active stores configured');
    }

    return {
      code: match.code,
      name: match.name,
      subtitle: `Floor and back-room stock, transfers, and replenishment — ${match.name}.`,
    };
  }

  private async buildAllStoreQtyMaps(): Promise<AllStoresQty> {
    type AggRow = { _id: { sku: string; storeId: string }; qty: number };

    const rows = await this.ledgerModel.aggregate<AggRow>([
      { $addFields: { loc: { $ifNull: ['$locationKind', 'warehouse'] } } },
      { $match: { loc: 'store', storeId: { $exists: true, $nin: [null, ''] } } },
      { $group: { _id: { sku: '$sku', storeId: '$storeId' }, qty: { $sum: '$qtyDelta' } } },
    ]);

    const result: AllStoresQty = new Map();
    for (const row of rows) {
      const storeId = String(row._id.storeId);
      const sku = row._id.sku;
      if (!result.has(storeId)) result.set(storeId, new Map());
      const storeMap = result.get(storeId)!;
      storeMap.set(sku, (storeMap.get(sku) ?? 0) + row.qty);
    }
    return result;
  }

  private async loadProductsForDashboard(): Promise<ProductLean[]> {
    return await this.productModel
      .find({ isActive: true })
      .select(
        'sku itemName categoryId storePrice sellingPrice minimumShelfFit minStock reorderLevel isActive',
      )
      .lean();
  }

  private computeStoreMetrics(
    storeQty: StoreSkuMap,
    products: ProductLean[],
    lowStockSkus: number,
  ): StoreMetricsSnapshot {
    const productBySku = new Map(products.map((p) => [p.sku, p]));
    let totalSkus = 0;
    let onShelfUnits = 0;
    let retailValue = 0;

    for (const [sku, qty] of storeQty) {
      if (qty <= 0) continue;
      totalSkus++;
      onShelfUnits += qty;
      const p = productBySku.get(sku);
      const price =
        (typeof p?.storePrice === 'number' ? p.storePrice : undefined) ??
        (typeof p?.sellingPrice === 'number' ? p.sellingPrice : undefined) ??
        0;
      retailValue += qty * price;
    }

    return { totalSkus, onShelfUnits, retailValue, lowStockSkus };
  }

  private getShelfThreshold(p: ProductLean): number | undefined {
    if (typeof p.minimumShelfFit === 'number') return p.minimumShelfFit;
    if (typeof p.minStock === 'number') return p.minStock;
    if (typeof p.reorderLevel === 'number') return p.reorderLevel;
    return undefined;
  }

  private evaluateLowStock(
    storeQty: StoreSkuMap,
    products: ProductLean[],
    limit: number,
  ): LowStockEval {
    const rows: StoreLowStockRow[] = [];

    for (const p of products) {
      const threshold = this.getShelfThreshold(p);
      if (threshold === undefined) continue;

      const quantity = storeQty.get(p.sku) ?? 0;
      if (quantity > threshold) continue;

      const criticalLevel = typeof p.minStock === 'number' ? p.minStock : threshold;
      const status: 'critical' | 'low' = quantity <= criticalLevel ? 'critical' : 'low';

      rows.push({
        sku: p.sku,
        productName: p.itemName,
        quantity,
        status,
      });
    }

    rows.sort((a, b) => a.quantity - b.quantity);
    return { rows: rows.slice(0, limit), count: rows.length };
  }

  private buildStoreNetwork(
    stores: Array<{ code: string; name: string }>,
    allStoreQty: AllStoresQty,
    products: ProductLean[],
  ): StoreNetworkRow[] {
    return stores.map((s) => {
      const storeQty = allStoreQty.get(s.code) ?? new Map<string, number>();
      const lowStock = this.evaluateLowStock(storeQty, products, Number.MAX_SAFE_INTEGER);
      const metrics = this.computeStoreMetrics(storeQty, products, lowStock.count);
      const shelfFillPercent =
        metrics.totalSkus > 0
          ? Math.round(((metrics.totalSkus - lowStock.count) / metrics.totalSkus) * 100)
          : 100;

      return {
        code: s.code,
        name: s.name,
        shelfFillPercent,
        totalSkus: metrics.totalSkus,
        units: metrics.onShelfUnits,
        lowStockSkus: lowStock.count,
      };
    });
  }

  private async getCategoryMix(
    storeQty: StoreSkuMap,
    products: ProductLean[],
  ): Promise<StoreCategoryMixRow[]> {
    const skus = [...storeQty.entries()].filter(([, q]) => q > 0);
    if (skus.length === 0) return [];

    const productBySku = new Map(products.map((p) => [p.sku, p]));
    const categoryPieces = new Map<string, number>();
    let totalPieces = 0;

    for (const [sku, qty] of skus) {
      if (qty <= 0) continue;
      totalPieces += qty;
      const p = productBySku.get(sku);
      const catKey = p?.categoryId ? String(p.categoryId) : '__uncategorized__';
      categoryPieces.set(catKey, (categoryPieces.get(catKey) ?? 0) + qty);
    }

    const categoryIds = [...categoryPieces.keys()].filter(
      (k) => k !== '__uncategorized__' && Types.ObjectId.isValid(k),
    );
    const categories = await this.categoryModel
      .find({ _id: { $in: categoryIds.map((id) => new Types.ObjectId(id)) } })
      .select('name')
      .lean();
    const nameById = new Map(categories.map((c) => [String(c._id), c.name]));

    return [...categoryPieces.entries()]
      .map(([categoryId, pieces]) => ({
        categoryId: categoryId === '__uncategorized__' ? '' : categoryId,
        categoryName:
          categoryId === '__uncategorized__'
            ? 'Uncategorized'
            : (nameById.get(categoryId) ?? 'Uncategorized'),
        pieces,
        percent: totalPieces > 0 ? Math.round((pieces / totalPieces) * 100) : 0,
      }))
      .sort((a, b) => b.pieces - a.pieces);
  }

  private async sumInboundTransferPiecesByStore(): Promise<Map<string, number>> {
    const transfers = await this.stModel
      .find({
        direction: 'warehouse_to_store',
        status: { $in: [...INBOUND_TRANSFER_STATUSES] },
      })
      .select('toStoreId lines')
      .lean();

    const map = new Map<string, number>();
    for (const t of transfers) {
      if (!t.toStoreId) continue;
      const pieces = this.sumTransferQty(t.lines);
      map.set(t.toStoreId, (map.get(t.toStoreId) ?? 0) + pieces);
    }
    return map;
  }

  private async countOpenIntents(storeId: string): Promise<number> {
    return await this.intentModel.countDocuments({
      storeId,
      status: { $in: [...OPEN_INTENT_STATUSES] },
    });
  }

  private async getRecentActivity(
    storeId: string,
    options: StoreDashboardOptions,
    lowStockRows: StoreLowStockRow[],
    stores: Array<{ code: string; name: string }>,
  ): Promise<StoreRecentActivity[]> {
    const storeName = stores.find((s) => s.code === storeId)?.name ?? storeId;

    const [completedTransfers, pendingTransfers, intents] = await Promise.all([
      this.stModel
        .find({
          direction: 'warehouse_to_store',
          toStoreId: storeId,
          status: 'completed',
        })
        .sort({ updatedAt: -1 })
        .limit(options.activityLimit)
        .lean(),
      this.stModel
        .find({
          direction: 'warehouse_to_store',
          toStoreId: storeId,
          status: { $in: ['in_transit', 'awaiting_intake'] },
        })
        .sort({ updatedAt: -1 })
        .limit(options.activityLimit)
        .lean(),
      this.intentModel
        .find({ storeId, status: { $in: [...OPEN_INTENT_STATUSES] } })
        .sort({ updatedAt: -1 })
        .limit(options.activityLimit)
        .lean(),
    ]);

    const items: StoreRecentActivity[] = [];

    for (const st of completedTransfers) {
      const pieces = this.sumTransferQty(st.lines);
      const desc = this.firstLineDescription(st.lines);
      items.push({
        id: String(st._id),
        kind: 'transfer',
        title: `Transfer in ${st.transferNo}`,
        description: `Received ${pieces} pieces from central bridal warehouse${desc ? ` · ${desc}` : ''}`,
        occurredAt: this.toIso(this.docTimestamp(st)),
        status: 'COMPLETED',
      });
    }

    for (const st of pendingTransfers) {
      const pieces = this.sumTransferQty(st.lines);
      const desc = this.firstLineDescription(st.lines);
      items.push({
        id: String(st._id),
        kind: 'transfer',
        title: `Transfer in ${st.transferNo}`,
        description: `Inbound ${pieces} pieces from warehouse${desc ? ` · ${desc}` : ''}`,
        occurredAt: this.toIso(this.docTimestamp(st)),
        status: 'PENDING',
      });
    }

    for (const intent of intents) {
      const pieces = this.sumIntentQty(intent.lines);
      items.push({
        id: String(intent._id),
        kind: 'purchase_intent',
        title: `Reorder ${intent.intentNo}`,
        description: `${pieces} pieces requested · ${storeName}`,
        occurredAt: this.toIso(this.docTimestamp(intent)),
        status: 'OPEN',
      });
    }

    for (const row of lowStockRows.slice(0, 3)) {
      items.push({
        id: `alert-${row.sku}`,
        kind: 'alert',
        title: `Low stock: ${row.sku} (${row.productName})`,
        description: 'Below min shelf quantity — replenish from warehouse suggested',
        occurredAt: new Date().toISOString(),
        status: 'ALERT',
      });
    }

    items.sort((a, b) => new Date(b.occurredAt).getTime() - new Date(a.occurredAt).getTime());
    return items.slice(0, options.activityLimit);
  }

  private async getTransferSchedule(
    storeId: string,
    limit: number,
    stores: Array<{ code: string; name: string }>,
  ): Promise<StoreTransferScheduleRow[]> {
    const storeName = stores.find((s) => s.code === storeId)?.name ?? storeId;

    const transfers = await this.stModel
      .find({
        direction: 'warehouse_to_store',
        toStoreId: storeId,
        status: { $in: [...INBOUND_TRANSFER_STATUSES] },
      })
      .sort({ transferDate: 1, createdAt: 1 })
      .limit(limit)
      .lean();

    return transfers.map((t) => {
      const pieces = this.sumTransferQty(t.lines);
      const status = t.status as 'draft' | 'in_transit' | 'awaiting_intake';
      return {
        transferId: String(t._id),
        transferNo: t.transferNo,
        title: t.transferNo,
        description: `${storeName} · ${pieces} pcs`,
        expectedDate: t.transferDate ?? null,
        status,
      };
    });
  }

  private sumTransferQty(lines: Array<{ qty?: number }> | undefined): number {
    if (!lines?.length) return 0;
    return lines.reduce((sum, l) => sum + (l.qty ?? 0), 0);
  }

  private sumIntentQty(lines: Array<{ requestedQty?: number }> | undefined): number {
    if (!lines?.length) return 0;
    return lines.reduce((sum, l) => sum + (l.requestedQty ?? 0), 0);
  }

  private firstLineDescription(
    lines: Array<{ description?: string }> | undefined,
  ): string | undefined {
    const desc = lines?.[0]?.description?.trim();
    return desc || undefined;
  }

  private docTimestamp(doc: Record<string, unknown>): unknown {
    return doc.updatedAt ?? doc.createdAt;
  }

  private toIso(value: unknown): string {
    if (value instanceof Date) return value.toISOString();
    if (typeof value === 'string' || typeof value === 'number') {
      const d = new Date(value);
      if (!Number.isNaN(d.getTime())) return d.toISOString();
    }
    return new Date().toISOString();
  }
}
