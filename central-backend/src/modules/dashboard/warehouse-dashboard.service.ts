import { Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model, Types } from 'mongoose';
import { Category, CategoryDocument } from '../categories/schemas/category.schema';
import { GoodsReceipt, GoodsReceiptDocument } from '../goods-receipts/schemas/goods-receipt.schema';
import { InventoryService, WarehouseSkuQtyMaps, WarehouseSkuQtyScope } from '../inventory/inventory.service';
import { Location, LocationDocument } from '../locations/schemas/location.schema';
import { Product, ProductDocument } from '../products/schemas/product.schema';
import { PurchaseOrder, PurchaseOrderDocument } from '../purchase-orders/schemas/purchase-order.schema';
import { StockTransfer, StockTransferDocument } from '../stock-transfers/schemas/stock-transfer.schema';
import { Store, StoreDocument } from '../stores/schemas/store.schema';
import type {
  WarehouseDashboardOptions,
  WarehouseDashboardResponse,
  WarehouseLowStockRow,
  WarehouseRecentActivity,
} from './warehouse-dashboard.types';

type ResolvedWarehouse = {
  warehouse: { code: string; name: string; subtitle: string };
  locationId: string;
  legacyDefaultLocationId: string;
  legacyDefaultLocationCode: string;
  qtyScope?: WarehouseSkuQtyScope;
};

type LowStockEval = {
  rows: WarehouseLowStockRow[];
  count: number;
};

@Injectable()
export class WarehouseDashboardService {
  constructor(
    private readonly inventoryService: InventoryService,
    @InjectModel(Product.name) private readonly productModel: Model<ProductDocument>,
    @InjectModel(Category.name) private readonly categoryModel: Model<CategoryDocument>,
    @InjectModel(Location.name) private readonly locationModel: Model<LocationDocument>,
    @InjectModel(GoodsReceipt.name) private readonly grModel: Model<GoodsReceiptDocument>,
    @InjectModel(StockTransfer.name) private readonly stModel: Model<StockTransferDocument>,
    @InjectModel(PurchaseOrder.name) private readonly poModel: Model<PurchaseOrderDocument>,
    @InjectModel(Store.name) private readonly storeModel: Model<StoreDocument>,
  ) {}

  async getWarehouseDashboard(options: WarehouseDashboardOptions): Promise<WarehouseDashboardResponse> {
    const resolved = await this.resolveWarehouse(options.locationCode);

    const qtyMaps = await this.inventoryService.getWarehouseSkuQtyMaps(resolved.qtyScope);
    const lowStockEval = await this.evaluateLowStock(qtyMaps.warehouseBySku, options.lowStockLimit);

    const [
      stockByCategory,
      pendingActions,
      recentActivity,
      inboundPipeline,
      metricsExtras,
    ] = await Promise.all([
      this.getStockByCategory(qtyMaps.warehouseBySku),
      this.countPendingActions(resolved),
      this.getRecentActivity(options, lowStockEval.rows, resolved),
      this.getInboundPipeline(options.inboundDays),
      this.computeMetricsExtras(qtyMaps),
    ]);

    return {
      warehouse: resolved.warehouse,
      metrics: {
        totalSkus: metricsExtras.totalSkus,
        stockUnits: qtyMaps.stockUnits,
        stockValue: metricsExtras.stockValue,
        inTransitUnits: qtyMaps.inTransitUnits,
        lowStockSkus: lowStockEval.count,
        pendingActions,
      },
      stockByCategory,
      recentActivity,
      lowStock: lowStockEval.rows,
      inboundPipeline,
    };
  }

  private async resolveWarehouse(locationCode?: string): Promise<ResolvedWarehouse> {
    const defaultLoc = await this.locationModel
      .findOne({ isActive: true, type: 'warehouse' })
      .sort({ code: 1 })
      .lean();

    const code = locationCode?.trim().toLowerCase();
    if (code) {
      const loc = await this.locationModel.findOne({ code, isActive: true, type: 'warehouse' }).lean();
      if (!loc) {
        throw new NotFoundException(`Warehouse location '${locationCode}' not found`);
      }
      const legacyDefaultLocationId = defaultLoc ? String(defaultLoc._id) : String(loc._id);
      const legacyDefaultLocationCode = defaultLoc ? defaultLoc.code : loc.code;
      return {
        warehouse: {
          code: loc.code,
          name: loc.name,
          subtitle: `Bridal warehouse stock, receipts, outbound transfers to boutiques, and alerts — ${loc.name}.`,
        },
        locationId: String(loc._id),
        legacyDefaultLocationId,
        legacyDefaultLocationCode,
        qtyScope: {
          locationCode: loc.code,
          legacyDefaultLocationCode,
        },
      };
    }

    if (!defaultLoc) {
      throw new NotFoundException('No active warehouse location configured');
    }

    return {
      warehouse: {
        code: defaultLoc.code,
        name: defaultLoc.name,
        subtitle: `Bridal warehouse stock, receipts, outbound transfers to boutiques, and alerts — ${defaultLoc.name}.`,
      },
      locationId: String(defaultLoc._id),
      legacyDefaultLocationId: String(defaultLoc._id),
      legacyDefaultLocationCode: defaultLoc.code,
    };
  }

  private async computeMetricsExtras(qtyMaps: WarehouseSkuQtyMaps) {
    const skusWithStock = [...qtyMaps.warehouseBySku.entries()]
      .filter(([, q]) => q > 0)
      .map(([sku]) => sku);

    const totalSkus = skusWithStock.length;
    if (skusWithStock.length === 0) {
      return { totalSkus: 0, stockValue: 0 };
    }

    const products = await this.productModel
      .find({ sku: { $in: skusWithStock }, isActive: true })
      .select('sku costPrice')
      .lean();

    let stockValue = 0;
    for (const p of products) {
      const qty = qtyMaps.warehouseBySku.get(p.sku) ?? 0;
      if (qty <= 0) continue;
      const cost = typeof p.costPrice === 'number' && Number.isFinite(p.costPrice) ? p.costPrice : 0;
      stockValue += qty * cost;
    }

    return { totalSkus, stockValue };
  }

  private async getStockByCategory(warehouseBySku: Map<string, number>) {
    const skus = [...warehouseBySku.entries()].filter(([, q]) => q > 0);
    if (skus.length === 0) return [];

    const skuList = skus.map(([s]) => s);
    const products = await this.productModel
      .find({ sku: { $in: skuList } })
      .select('sku categoryId')
      .lean();

    const categoryPieces = new Map<string, number>();
    let totalPieces = 0;

    for (const p of products) {
      const qty = warehouseBySku.get(p.sku) ?? 0;
      if (qty <= 0) continue;
      totalPieces += qty;
      const catKey = p.categoryId ? String(p.categoryId) : '__uncategorized__';
      categoryPieces.set(catKey, (categoryPieces.get(catKey) ?? 0) + qty);
    }

    const uncategorizedQty = skus
      .filter(([sku]) => !products.some((p) => p.sku === sku))
      .reduce((sum, [, q]) => sum + q, 0);
    if (uncategorizedQty > 0) {
      totalPieces += uncategorizedQty;
      categoryPieces.set(
        '__uncategorized__',
        (categoryPieces.get('__uncategorized__') ?? 0) + uncategorizedQty,
      );
    }

    const categoryIds = [...categoryPieces.keys()].filter(
      (k) => k !== '__uncategorized__' && Types.ObjectId.isValid(k),
    );
    const categories = await this.categoryModel
      .find({ _id: { $in: categoryIds.map((id) => new Types.ObjectId(id)) } })
      .select('name')
      .lean();
    const nameById = new Map(categories.map((c) => [String(c._id), c.name]));

    const rows = [...categoryPieces.entries()]
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

    return rows;
  }

  private async evaluateLowStock(
    warehouseBySku: Map<string, number>,
    limit: number,
  ): Promise<LowStockEval> {
    const products = await this.productModel
      .find({
        isActive: true,
        $or: [
          { reorderLevel: { $exists: true, $ne: null } },
          { minStock: { $exists: true, $ne: null } },
        ],
      })
      .select('sku itemName reorderLevel minStock')
      .lean();

    const rows: WarehouseLowStockRow[] = [];

    for (const p of products) {
      const threshold =
        typeof p.reorderLevel === 'number'
          ? p.reorderLevel
          : typeof p.minStock === 'number'
            ? p.minStock
            : undefined;
      if (threshold === undefined) continue;

      const quantity = warehouseBySku.get(p.sku) ?? 0;
      if (quantity > threshold) continue;

      const criticalLevel =
        typeof p.minStock === 'number' ? p.minStock : threshold;
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

  private async countPendingActions(resolved: ResolvedWarehouse): Promise<number> {
    const [draftGrn, openTransfers] = await Promise.all([
      this.grModel.countDocuments({ status: 'draft' }),
      this.stModel.countDocuments({
        ...this.transferFilterForWarehouse(resolved),
        status: { $in: ['draft', 'in_transit', 'awaiting_intake'] },
      }),
    ]);
    return draftGrn + openTransfers;
  }

  private transferFilterForWarehouse(resolved: ResolvedWarehouse): Record<string, unknown> {
    const locationMatch = this.locationMatchOrLegacy(
      resolved.locationId,
      resolved.legacyDefaultLocationId,
      'fromLocationId',
    );
    return {
      direction: 'warehouse_to_store',
      ...locationMatch,
    };
  }

  private locationMatchOrLegacy(
    locationId: string,
    legacyDefaultLocationId: string,
    field: 'fromLocationId',
  ): Record<string, unknown> {
    if (locationId === legacyDefaultLocationId) {
      return {
        $or: [
          { [field]: new Types.ObjectId(locationId) },
          { [field]: { $exists: false } },
          { [field]: null },
        ],
      };
    }
    return { [field]: new Types.ObjectId(locationId) };
  }

  private async getRecentActivity(
    options: WarehouseDashboardOptions,
    lowStockRows: WarehouseLowStockRow[],
    resolved: ResolvedWarehouse,
  ): Promise<WarehouseRecentActivity[]> {
    const transferBase = this.transferFilterForWarehouse(resolved);
    const [grns, transfers, pos] = await Promise.all([
      this.grModel
        .find({ status: 'posted' })
        .sort({ updatedAt: -1 })
        .limit(options.activityLimit)
        .lean(),
      this.stModel
        .find({
          ...transferBase,
          status: { $in: ['in_transit', 'awaiting_intake'] },
        })
        .sort({ updatedAt: -1 })
        .limit(options.activityLimit)
        .lean(),
      this.poModel
        .find({ status: { $in: ['open', 'approved', 'partially_received'] } })
        .sort({ updatedAt: -1 })
        .limit(options.activityLimit)
        .lean(),
    ]);

    const storeCodes = [
      ...new Set(transfers.map((t) => t.toStoreId).filter((id): id is string => Boolean(id))),
    ];
    const stores = await this.storeModel.find({ code: { $in: storeCodes } }).lean();
    const storeNameByCode = new Map(stores.map((s) => [s.code, s.name]));

    const items: WarehouseRecentActivity[] = [];

    for (const gr of grns) {
      const pieces = this.sumGrnReceivedQty(gr.lines);
      const desc = this.firstLineDescription(gr.lines);
      items.push({
        id: String(gr._id),
        kind: 'grn',
        title: `GRN-${gr.grnNumber ?? gr.receiptNo} · ${gr.supplier?.name ?? 'Supplier'}`,
        description: `Received ${pieces} pieces${desc ? ` · ${desc}` : ''}`,
        occurredAt: this.toIso(this.docTimestamp(gr)),
        status: 'COMPLETED',
      });
    }

    for (const st of transfers) {
      const pieces = this.sumTransferQty(st.lines);
      const desc = this.firstLineDescription(st.lines);
      const storeName = st.toStoreId ? (storeNameByCode.get(st.toStoreId) ?? st.toStoreId) : 'Store';
      items.push({
        id: String(st._id),
        kind: 'transfer',
        title: `STO-${st.transferNo} → ${storeName}`,
        description: `Dispatched ${pieces} pieces${desc ? ` · ${desc}` : ''}`,
        occurredAt: this.toIso(this.docTimestamp(st)),
        status: 'PENDING',
      });
    }

    for (const po of pos) {
      const pieces = this.sumPoPieces(po.lines);
      const desc = this.firstLineDescription(po.lines);
      const expected = po.deliveryDate ? `Expected ${po.deliveryDate}` : 'Awaiting receipt';
      items.push({
        id: String(po._id),
        kind: 'purchase_order',
        title: `PO-${po.poNo} · ${po.supplier?.name ?? po.supplier?.shortname ?? 'Supplier'}`,
        description: `${expected}${desc ? ` · ${desc}` : ''}`,
        occurredAt: this.toIso(this.docTimestamp(po)),
        status: 'OPEN',
      });
    }

    for (const row of lowStockRows.slice(0, 3)) {
      items.push({
        id: `alert-${row.sku}`,
        kind: 'alert',
        title: `Low stock: ${row.sku} (${row.productName})`,
        description: 'Central warehouse below reorder — reorder to supplier suggested',
        occurredAt: new Date().toISOString(),
        status: 'ALERT',
      });
    }

    items.sort((a, b) => new Date(b.occurredAt).getTime() - new Date(a.occurredAt).getTime());
    return items.slice(0, options.activityLimit);
  }

  private async getInboundPipeline(inboundDays: number) {
    const { from, to } = this.inboundDateRange(inboundDays);

    const docs = await this.poModel
      .find({
        status: { $in: ['open', 'approved', 'partially_received'] },
        deliveryDate: { $gte: from, $lte: to },
      })
      .sort({ deliveryDate: 1 })
      .limit(20)
      .lean();

    return docs.map((po) => ({
      poId: String(po._id),
      poNo: po.poNo,
      supplierName: po.supplier?.name ?? po.supplier?.shortname ?? 'Unknown',
      totalPieces: this.sumPoPieces(po.lines),
      expectedDate: po.deliveryDate ?? null,
    }));
  }

  private inboundDateRange(days: number): { from: string; to: string } {
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const end = new Date(today);
    end.setDate(end.getDate() + days);
    return {
      from: this.formatDateYmd(today),
      to: this.formatDateYmd(end),
    };
  }

  private formatDateYmd(d: Date): string {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }

  private sumGrnReceivedQty(lines: Array<{ receivedQty?: number }> | undefined): number {
    if (!lines?.length) return 0;
    return lines.reduce((sum, l) => sum + (l.receivedQty ?? 0), 0);
  }

  private sumTransferQty(lines: Array<{ qty?: number }> | undefined): number {
    if (!lines?.length) return 0;
    return lines.reduce((sum, l) => sum + (l.qty ?? 0), 0);
  }

  private sumPoPieces(
    lines: Array<{ recdQty?: number; freeQty?: number }> | undefined,
  ): number {
    if (!lines?.length) return 0;
    return lines.reduce((sum, l) => {
      const ordered = (l.recdQty ?? 0) + (l.freeQty ?? 0);
      return sum + (ordered > 0 ? ordered : 1);
    }, 0);
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
