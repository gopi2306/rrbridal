import { BadRequestException, Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { enrichProductDocuments } from '../../common/product-line-enrichment';
import { Product, ProductDocument } from '../products/schemas/product.schema';
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
    @InjectModel(Product.name) private readonly productModel: Model<ProductDocument>,
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

    const enrichedProducts = await enrichProductDocuments(
      this.productModel,
      products as Array<Record<string, unknown>>,
    );

    const skus = enrichedProducts
      .map((p) => (typeof p.sku === 'string' ? p.sku : ''))
      .filter(Boolean);
    const balanceMap = await this.aggregateBalancesForSkus(skus);

    const data = enrichedProducts.map((p) => {
      const sku = typeof p.sku === 'string' ? p.sku : '';
      const b = balanceMap.get(sku) ?? { warehouseQty: 0, inTransitQty: 0, storeById: new Map<string, number>() };
      const storeQty = this.sumStoreQty(b.storeById, params.storeId);
      const mrp = typeof p.mrp === 'number' ? p.mrp : undefined;
      const storePrice =
        (typeof p.storePrice === 'number' ? p.storePrice : undefined) ??
        (typeof p.sellingPrice === 'number' ? p.sellingPrice : undefined);
      return {
        sku,
        productId: p._id != null ? String(p._id) : undefined,
        upcEanCode: typeof p.upcEanCode === 'string' ? p.upcEanCode : undefined,
        product: p,
        warehouseQty: b.warehouseQty,
        inTransitQty: b.inTransitQty,
        storeQty,
        mrp,
        storePrice,
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
