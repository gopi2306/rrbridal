import { BadRequestException, Injectable, PayloadTooLargeException } from '@nestjs/common';
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

export type WarehouseStoreGridRow = {
  sku: string;
  productId?: string;
  upcEanCode?: string;
  product: Record<string, unknown>;
  warehouseQty: number;
  inTransitQty: number;
  storeQty: number;
  mrp?: number;
  storePrice?: number;
};

type BalanceBucket = { warehouseQty: number; inTransitQty: number; storeById: Map<string, number> };

const EXPORT_MAX_ROWS = 10_000;
const EXPORT_PAGE_SIZE = 500;

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
    const listParams = this.buildListParams(params.search);
    const page = Math.max(1, params.page ?? 1);
    const limit = Math.min(500, Math.max(1, params.limit ?? 200));
    const skip = (page - 1) * limit;

    const [data, total] = await Promise.all([
      this.fetchWarehouseStoreRows({
        ...(params.search !== undefined && params.search !== '' ? { search: params.search } : {}),
        ...(params.storeId !== undefined && params.storeId !== '' ? { storeId: params.storeId } : {}),
        skip,
        limit,
      }),
      this.productsService.countForListFilter(listParams),
    ]);

    return {
      data,
      total,
      page,
      limit,
      totalPages: Math.ceil(total / limit),
    };
  }

  /** All matching grid rows for export (paginated internally, capped at maxRows). */
  async fetchAllWarehouseStoreRows(params: {
    search?: string;
    storeId?: string;
    maxRows?: number;
  }): Promise<WarehouseStoreGridRow[]> {
    const maxRows = params.maxRows ?? EXPORT_MAX_ROWS;
    const listParams = this.buildListParams(params.search);
    const total = await this.productsService.countForListFilter(listParams);
    if (total > maxRows) {
      throw new PayloadTooLargeException(
        `Export exceeds maximum of ${maxRows} rows (${total} match). Narrow filters and try again.`,
      );
    }

    const all: WarehouseStoreGridRow[] = [];
    let skip = 0;
    const fetchParams = {
      ...(params.search !== undefined && params.search !== '' ? { search: params.search } : {}),
      ...(params.storeId !== undefined && params.storeId !== '' ? { storeId: params.storeId } : {}),
    };
    while (true) {
      const batch = await this.fetchWarehouseStoreRows({
        ...fetchParams,
        skip,
        limit: EXPORT_PAGE_SIZE,
      });
      if (batch.length === 0) break;
      all.push(...batch);
      if (batch.length < EXPORT_PAGE_SIZE) break;
      skip += EXPORT_PAGE_SIZE;
    }
    return all;
  }

  async fetchWarehouseStoreRows(params: {
    search?: string;
    storeId?: string;
    skip?: number;
    limit?: number;
  }): Promise<WarehouseStoreGridRow[]> {
    const listParams = this.buildListParams(params.search);
    const skip = Math.max(0, params.skip ?? 0);
    const limit = Math.min(500, Math.max(1, params.limit ?? 200));

    const products = await this.productsService.list({ ...listParams, skip, limit });
    if (products.length === 0) return [];

    const enrichedProducts = await enrichProductDocuments(
      this.productModel,
      products as Array<Record<string, unknown>>,
    );

    const skus = enrichedProducts
      .map((p) => (typeof p.sku === 'string' ? p.sku : ''))
      .filter(Boolean);
    const balanceMap = await this.aggregateBalancesForSkus(skus);

    return enrichedProducts.map((p) => this.mapProductToGridRow(p, balanceMap, params.storeId));
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

  private buildListParams(search?: string): { search?: string } {
    const listParams: { search?: string } = {};
    if (search !== undefined && search !== '') listParams.search = search;
    return listParams;
  }

  private mapProductToGridRow(
    p: Record<string, unknown>,
    balanceMap: Map<string, BalanceBucket>,
    filterStoreId?: string,
  ): WarehouseStoreGridRow {
    const sku = typeof p.sku === 'string' ? p.sku : '';
    const b = balanceMap.get(sku) ?? { warehouseQty: 0, inTransitQty: 0, storeById: new Map<string, number>() };
    const storeQty = this.sumStoreQty(b.storeById, filterStoreId);
    const storePrice =
      (typeof p.storePrice === 'number' ? p.storePrice : undefined) ??
      (typeof p.sellingPrice === 'number' ? p.sellingPrice : undefined);
    const row: WarehouseStoreGridRow = {
      sku,
      product: p,
      warehouseQty: b.warehouseQty,
      inTransitQty: b.inTransitQty,
      storeQty,
    };
    if (p._id != null) row.productId = String(p._id);
    if (typeof p.upcEanCode === 'string') row.upcEanCode = p.upcEanCode;
    if (typeof p.mrp === 'number') row.mrp = p.mrp;
    if (storePrice !== undefined) row.storePrice = storePrice;
    return row;
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
