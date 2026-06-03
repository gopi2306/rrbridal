import { BadRequestException, Injectable, PayloadTooLargeException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model, Types } from 'mongoose';
import { enrichProductDocuments } from '../../common/product-line-enrichment';
import { Location, LocationDocument } from '../locations/schemas/location.schema';
import { Product, ProductDocument } from '../products/schemas/product.schema';
import { ProductsService, ProductListFilterParams } from '../products/products.service';
import { InventoryLedgerEntry, InventoryLedgerDocument, InventoryLocationKind } from './schemas/inventory-ledger.schema';

export type LedgerEntryInput = {
  sku: string;
  qtyDelta: number;
  sourceType: string;
  sourceId: string;
  note?: string;
  locationKind?: InventoryLocationKind;
  storeId?: string;
  locationCode?: string;
};

export type WarehouseSkuQtyMaps = {
  warehouseBySku: Map<string, number>;
  inTransitBySku: Map<string, number>;
  stockUnits: number;
  inTransitUnits: number;
};

export type WarehouseSkuQtyScope = {
  locationCode: string;
  /** Legacy ledger rows without locationCode count only against this location */
  legacyDefaultLocationCode: string;
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
    @InjectModel(Location.name) private readonly locationModel: Model<LocationDocument>,
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
      if (loc === 'store' && e.storeId) row.storeId = String(e.storeId).trim().toLowerCase();
      if (loc === 'warehouse' || loc === 'in_transit') {
        const code = e.locationCode?.trim().toLowerCase();
        if (code) row.locationCode = code;
      }
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
    const storeId = params.storeId?.trim().toLowerCase();

    if (storeId) {
      const storeQtyMap = await this.getStoreSkuQtyMap(storeId);
      const skusWithStock = [...storeQtyMap.entries()]
        .filter(([, qty]) => qty > 0)
        .map(([sku]) => sku);
      const scopedListParams = {
        ...listParams,
        skus: skusWithStock.length > 0 ? skusWithStock : ['__no_store_stock__'],
      };

      const [data, total] = await Promise.all([
        this.fetchWarehouseStoreRows({
          ...(params.search !== undefined && params.search !== '' ? { search: params.search } : {}),
          storeId,
          skip,
          limit,
        }),
        this.productsService.countForListFilter(scopedListParams),
      ]);

      return {
        data,
        total,
        page,
        limit,
        totalPages: Math.ceil(total / limit),
      };
    }

    const [data, total] = await Promise.all([
      this.fetchWarehouseStoreRows({
        ...(params.search !== undefined && params.search !== '' ? { search: params.search } : {}),
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
    const storeId = params.storeId?.trim().toLowerCase();

    let countParams = listParams;
    if (storeId) {
      const storeQtyMap = await this.getStoreSkuQtyMap(storeId);
      const skusWithStock = [...storeQtyMap.entries()]
        .filter(([, qty]) => qty > 0)
        .map(([sku]) => sku);
      countParams = {
        ...listParams,
        skus: skusWithStock.length > 0 ? skusWithStock : ['__no_store_stock__'],
      };
    }

    const total = await this.productsService.countForListFilter(countParams);
    if (total > maxRows) {
      throw new PayloadTooLargeException(
        `Export exceeds maximum of ${maxRows} rows (${total} match). Narrow filters and try again.`,
      );
    }

    const all: WarehouseStoreGridRow[] = [];
    let skip = 0;
    const fetchParams = {
      ...(params.search !== undefined && params.search !== '' ? { search: params.search } : {}),
      ...(storeId ? { storeId } : {}),
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
    const storeId = params.storeId?.trim().toLowerCase();

    if (storeId) {
      const storeQtyMap = await this.getStoreSkuQtyMap(storeId);
      const skusWithStock = [...storeQtyMap.entries()]
        .filter(([, qty]) => qty > 0)
        .map(([sku]) => sku);
      if (skusWithStock.length === 0) return [];

      const products = await this.productsService.list({
        ...listParams,
        skus: skusWithStock,
        skip,
        limit,
      });
      if (products.length === 0) return [];

      const enrichedProducts = await enrichProductDocuments(
        this.productModel,
        products as Array<Record<string, unknown>>,
      );

      return enrichedProducts.map((p) =>
        this.mapProductToGridRow(p, new Map(), storeId, {
          storeQtyOverride: storeQtyMap.get(typeof p.sku === 'string' ? p.sku : '') ?? 0,
          storeOnly: true,
        }),
      );
    }

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

    return enrichedProducts.map((p) => this.mapProductToGridRow(p, balanceMap));
  }

  /** Warehouse on-hand qty per SKU (global pool from ledger `locationKind: warehouse`). */
  async getWarehouseQtyBySkus(skus: string[]): Promise<Map<string, number>> {
    const maps = await this.getWarehouseSkuQtyMaps();
    const trimmed = [...new Set(skus.map((s) => s.trim()).filter(Boolean))];
    const result = new Map<string, number>();
    for (const sku of trimmed) {
      result.set(sku, maps.warehouseBySku.get(sku) ?? 0);
    }
    return result;
  }

  /** Warehouse on-hand qty per SKU at a single warehouse location. */
  async getWarehouseQtyBySkusAtLocation(
    locationCode: string,
    skus: string[],
    legacyDefaultLocationCode: string,
  ): Promise<Map<string, number>> {
    const maps = await this.getWarehouseSkuQtyMaps({
      locationCode: locationCode.trim().toLowerCase(),
      legacyDefaultLocationCode: legacyDefaultLocationCode.trim().toLowerCase(),
    });
    const trimmed = [...new Set(skus.map((s) => s.trim()).filter(Boolean))];
    const result = new Map<string, number>();
    for (const sku of trimmed) {
      result.set(sku, maps.warehouseBySku.get(sku) ?? 0);
    }
    return result;
  }

  /** First active warehouse location code (for legacy GRN posting and default dashboard scope). */
  async getDefaultWarehouseLocationCode(): Promise<string | undefined> {
    const loc = await this.locationModel
      .findOne({ isActive: true, type: 'warehouse' })
      .sort({ code: 1 })
      .select('code')
      .lean();
    return loc?.code?.trim().toLowerCase() || undefined;
  }

  /**
   * Aggregates warehouse and in-transit quantities.
   * Without scope: global totals (all locations, including legacy rows without locationCode).
   * With scope: only rows for that warehouse, plus legacy rows attributed to legacyDefaultLocationCode.
   */
  async getWarehouseSkuQtyMaps(scope?: WarehouseSkuQtyScope): Promise<WarehouseSkuQtyMaps> {
    type AggRow = {
      _id: { sku: string; loc: string; locationCode: string | null };
      qty: number;
    };

    const rows = await this.ledgerModel.aggregate<AggRow>([
      { $addFields: { loc: { $ifNull: ['$locationKind', 'warehouse'] } } },
      { $match: { loc: { $in: ['warehouse', 'in_transit'] } } },
      {
        $group: {
          _id: {
            sku: '$sku',
            loc: '$loc',
            locationCode: { $ifNull: ['$locationCode', null] },
          },
          qty: { $sum: '$qtyDelta' },
        },
      },
    ]);

    const warehouseBySku = new Map<string, number>();
    const inTransitBySku = new Map<string, number>();
    let stockUnits = 0;
    let inTransitUnits = 0;

    for (const row of rows) {
      if (scope && !this.rowMatchesWarehouseScope(row._id.locationCode, scope)) continue;

      const sku = row._id.sku;
      const qty = row.qty;
      if (row._id.loc === 'warehouse') {
        warehouseBySku.set(sku, (warehouseBySku.get(sku) ?? 0) + qty);
        if (qty > 0) stockUnits += qty;
      } else if (row._id.loc === 'in_transit') {
        inTransitBySku.set(sku, (inTransitBySku.get(sku) ?? 0) + qty);
        if (qty > 0) inTransitUnits += qty;
      }
    }

    return { warehouseBySku, inTransitBySku, stockUnits, inTransitUnits };
  }

  private rowMatchesWarehouseScope(
    rowLocationCode: string | null,
    scope: WarehouseSkuQtyScope,
  ): boolean {
    const normalized = rowLocationCode?.trim().toLowerCase() ?? null;
    if (!normalized) {
      return scope.locationCode === scope.legacyDefaultLocationCode;
    }
    return normalized === scope.locationCode;
  }

  /** Store on-hand qty per SKU for a single store (ledger `locationKind: store`). */
  async getStoreQtyBySkus(storeId: string, skus: string[]): Promise<Map<string, number>> {
    const sid = storeId.trim().toLowerCase();
    const trimmed = [...new Set(skus.map((s) => s.trim()).filter(Boolean))];
    const balanceMap = await this.aggregateBalancesForSkus(trimmed);
    const result = new Map<string, number>();
    for (const sku of trimmed) {
      result.set(sku, balanceMap.get(sku)?.storeById.get(sid) ?? 0);
    }
    return result;
  }

  /** All on-hand SKU quantities for one store. */
  async getStoreSkuQtyMap(storeId: string): Promise<Map<string, number>> {
    const sid = storeId.trim().toLowerCase();
    type AggRow = { _id: string; qty: number };

    const rows = await this.ledgerModel.aggregate<AggRow>([
      { $addFields: { loc: { $ifNull: ['$locationKind', 'warehouse'] } } },
      { $match: { loc: 'store', storeId: { $exists: true, $nin: [null, ''] } } },
      {
        $addFields: {
          normalizedStoreId: { $toLower: { $trim: { input: { $toString: '$storeId' } } } },
        },
      },
      { $match: { normalizedStoreId: sid } },
      { $group: { _id: '$sku', qty: { $sum: '$qtyDelta' } } },
    ]);

    const map = new Map<string, number>();
    for (const row of rows) {
      if (row.qty !== 0) map.set(row._id, row.qty);
    }
    return map;
  }

  /** On-hand SKU quantities grouped by normalized store code (for multi-store views). */
  async getAllStoreSkuQtyMaps(): Promise<Map<string, Map<string, number>>> {
    type AggRow = { _id: { sku: string; storeId: string }; qty: number };

    const rows = await this.ledgerModel.aggregate<AggRow>([
      { $addFields: { loc: { $ifNull: ['$locationKind', 'warehouse'] } } },
      { $match: { loc: 'store', storeId: { $exists: true, $nin: [null, ''] } } },
      {
        $addFields: {
          normalizedStoreId: { $toLower: { $trim: { input: { $toString: '$storeId' } } } },
        },
      },
      { $group: { _id: { sku: '$sku', storeId: '$normalizedStoreId' }, qty: { $sum: '$qtyDelta' } } },
    ]);

    const result = new Map<string, Map<string, number>>();
    for (const row of rows) {
      const storeKey = row._id.storeId;
      const sku = row._id.sku;
      if (!result.has(storeKey)) result.set(storeKey, new Map());
      const storeMap = result.get(storeKey)!;
      if (row.qty !== 0) storeMap.set(sku, row.qty);
    }
    return result;
  }

  private buildListParams(search?: string): ProductListFilterParams {
    const listParams: ProductListFilterParams = {};
    if (search !== undefined && search !== '') listParams.search = search;
    return listParams;
  }

  private mapProductToGridRow(
    p: Record<string, unknown>,
    balanceMap: Map<string, BalanceBucket>,
    filterStoreId?: string,
    opts?: { storeQtyOverride?: number; storeOnly?: boolean },
  ): WarehouseStoreGridRow {
    const sku = typeof p.sku === 'string' ? p.sku : '';
    const b = balanceMap.get(sku) ?? { warehouseQty: 0, inTransitQty: 0, storeById: new Map<string, number>() };
    const storeQty = opts?.storeQtyOverride ?? this.sumStoreQty(b.storeById, filterStoreId);
    const storePrice =
      (typeof p.storePrice === 'number' ? p.storePrice : undefined) ??
      (typeof p.sellingPrice === 'number' ? p.sellingPrice : undefined);
    const row: WarehouseStoreGridRow = {
      sku,
      product: p,
      warehouseQty: opts?.storeOnly ? 0 : b.warehouseQty,
      inTransitQty: opts?.storeOnly ? 0 : b.inTransitQty,
      storeQty,
    };
    if (p._id != null) row.productId = String(p._id);
    if (typeof p.upcEanCode === 'string') row.upcEanCode = p.upcEanCode;
    if (typeof p.mrp === 'number') row.mrp = p.mrp;
    if (storePrice !== undefined) row.storePrice = storePrice;
    return row;
  }

  private sumStoreQty(storeById: Map<string, number>, filterStoreId?: string): number {
    if (filterStoreId) return storeById.get(filterStoreId.trim().toLowerCase()) ?? 0;
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
        const sid = row._id.storeId ? String(row._id.storeId).trim().toLowerCase() : '';
        b.storeById.set(sid, (b.storeById.get(sid) ?? 0) + row.qty);
      }
    }

    return map;
  }
}
