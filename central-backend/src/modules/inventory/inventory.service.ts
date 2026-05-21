import { BadRequestException, Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { ProductsService } from '../products/products.service';
import { InventoryLedgerEntry, InventoryLedgerDocument, InventoryLocationKind } from './schemas/inventory-ledger.schema';

export type LedgerEntryInput = {
  sku: string;
  qtyDelta: number;
  sourceType: string;
  sourceId: string;
  note?: string;
  locationKind?: InventoryLocationKind;
  storeId?: string;
};

type BalanceBucket = { warehouseQty: number; inTransitQty: number; storeById: Map<string, number> };

@Injectable()
export class InventoryService {
  constructor(
    @InjectModel(InventoryLedgerEntry.name) private readonly ledgerModel: Model<InventoryLedgerDocument>,
    private readonly productsService: ProductsService,
  ) {}

  async addLedgerEntries(entries: LedgerEntryInput[]) {
    if (entries.length === 0) return [];
    const docs = entries.map((e) => {
      const loc: InventoryLocationKind = e.locationKind ?? 'warehouse';
      if (loc === 'store' && (!e.storeId || !String(e.storeId).trim())) {
        throw new BadRequestException('storeId is required for store location ledger entries');
      }
      const row: Record<string, unknown> = {
        sku: e.sku.trim(),
        qtyDelta: e.qtyDelta,
        sourceType: e.sourceType,
        sourceId: e.sourceId,
        locationKind: loc,
      };
      if (e.note !== undefined) row.note = e.note;
      if (loc === 'store' && e.storeId) row.storeId = String(e.storeId).trim();
      return row;
    });
    return await this.ledgerModel.insertMany(docs);
  }

  /**
   * Centralized warehouse + store quantities per SKU, merged with product master (MRP, store price, names).
   */
  async getWarehouseStoreGrid(params: { search?: string; storeId?: string; page?: number; limit?: number }) {
    const listParams: { search?: string } = {};
    if (params.search !== undefined && params.search !== '') listParams.search = params.search;

    const page = Math.max(1, params.page ?? 1);
    const limit = Math.min(500, Math.max(1, params.limit ?? 200));
    const skip = (page - 1) * limit;

    const [products, total] = await Promise.all([
      this.productsService.list({ ...listParams, skip, limit }),
      this.productsService.countForListFilter(listParams),
    ]);

    if (products.length === 0) {
      return {
        data: [],
        total,
        page,
        limit,
        totalPages: Math.ceil(total / limit),
      };
    }

    const skus = products.map((p) => p.sku);
    const balanceMap = await this.aggregateBalancesForSkus(skus);

    const data = products.map((p) => {
      const b = balanceMap.get(p.sku) ?? { warehouseQty: 0, inTransitQty: 0, storeById: new Map<string, number>() };
      const storeQty = this.sumStoreQty(b.storeById, params.storeId);
      return {
        sku: p.sku,
        upcEanCode: p.upcEanCode,
        product: p.itemName,
        warehouseQty: b.warehouseQty,
        inTransitQty: b.inTransitQty,
        storeQty,
        mrp: p.mrp,
        storePrice: p.storePrice ?? p.sellingPrice,
      };
    });

    return {
      data,
      total,
      page,
      limit,
      totalPages: Math.ceil(total / limit),
    };
  }

  /** Warehouse on-hand qty per SKU (global pool from ledger `locationKind: warehouse`). */
  async getWarehouseQtyBySkus(skus: string[]): Promise<Map<string, number>> {
    const trimmed = [...new Set(skus.map((s) => s.trim()).filter(Boolean))];
    const balanceMap = await this.aggregateBalancesForSkus(trimmed);
    const result = new Map<string, number>();
    for (const sku of trimmed) {
      result.set(sku, balanceMap.get(sku)?.warehouseQty ?? 0);
    }
    return result;
  }

  /** Store on-hand qty per SKU for a single store (ledger `locationKind: store`). */
  async getStoreQtyBySkus(storeId: string, skus: string[]): Promise<Map<string, number>> {
    const sid = storeId.trim();
    const trimmed = [...new Set(skus.map((s) => s.trim()).filter(Boolean))];
    const balanceMap = await this.aggregateBalancesForSkus(trimmed);
    const result = new Map<string, number>();
    for (const sku of trimmed) {
      result.set(sku, balanceMap.get(sku)?.storeById.get(sid) ?? 0);
    }
    return result;
  }

  private sumStoreQty(storeById: Map<string, number>, filterStoreId?: string): number {
    if (filterStoreId) return storeById.get(filterStoreId) ?? 0;
    let sum = 0;
    for (const q of storeById.values()) sum += q;
    return sum;
  }

  private async aggregateBalancesForSkus(skus: string[]): Promise<Map<string, BalanceBucket>> {
    const map = new Map<string, BalanceBucket>();
    if (skus.length === 0) return map;

    type AggRow = {
      _id: { sku: string; loc: string; storeId: string | null };
      qty: number;
    };

    const rows = await this.ledgerModel.aggregate<AggRow>([
      { $match: { sku: { $in: skus } } },
      { $addFields: { loc: { $ifNull: ['$locationKind', 'warehouse'] } } },
      {
        $group: {
          _id: { sku: '$sku', loc: '$loc', storeId: { $ifNull: ['$storeId', null] } },
          qty: { $sum: '$qtyDelta' },
        },
      },
    ]);

    for (const row of rows) {
      const sku = row._id.sku;
      if (!map.has(sku)) map.set(sku, { warehouseQty: 0, inTransitQty: 0, storeById: new Map() });
      const b = map.get(sku)!;
      if (row._id.loc === 'warehouse') {
        b.warehouseQty += row.qty;
      } else if (row._id.loc === 'in_transit') {
        b.inTransitQty += row.qty;
      } else if (row._id.loc === 'store') {
        const sid = row._id.storeId ? String(row._id.storeId) : '';
        b.storeById.set(sid, (b.storeById.get(sid) ?? 0) + row.qty);
      }
    }

    return map;
  }
}
