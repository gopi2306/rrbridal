import { BadRequestException, Injectable, NotFoundException, PayloadTooLargeException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model } from 'mongoose';
import { applyObjectIdRefFilter, coerceObjectIdString } from '../../common/object-id.util';
import { roundMoney } from '../../common/money.util';
import { enrichProductDocuments } from '../../common/product-line-enrichment';
import { Location, LocationDocument } from '../locations/schemas/location.schema';
import { Product, ProductDocument } from '../products/schemas/product.schema';
import { ProductsService, ProductListFilterParams } from '../products/products.service';
import { Store, StoreDocument } from '../stores/schemas/store.schema';
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

export type FilteredInventoryParams = {
  storeCode?: string;
  search?: string;
  departmentId?: string;
  categoryId?: string;
  subCategoryId?: string;
  supplierId?: string;
  minQty?: number;
  maxQty?: number;
  minAgeDays?: number;
  maxAgeDays?: number;
  fromDate?: string;
  toDate?: string;
  page?: number;
  limit?: number;
};

export type FilteredInventorySummary = {
  inventoryKind: 'warehouse' | 'store';
  storeCode?: string;
  filteredSkuCount: number;
  warehouseQty: number;
  inTransitQty: number;
  storeQty: number;
  supplierCount: number;
  sellingAmount: number;
  costAmount: number;
};

export type FilteredInventoryRow = {
  sku: string;
  productId?: string;
  upcEanCode?: string;
  product: Record<string, unknown>;
  inventoryKind: 'warehouse' | 'store';
  storeCode?: string;
  stockQty: number;
  warehouseQty: number;
  inTransitQty: number;
  storeQty: number;
  stockAgeDays: number | null;
  mrp?: number;
  storePrice?: number;
};

export type ProductInventoryStoreDetail = {
  storeCode: string;
  storeName: string | null;
  storeQty: number;
  ageDays: number | null;
};

export type ProductInventoryDetail = {
  product: Record<string, unknown>;
  warehouse: {
    warehouseQty: number;
    inTransitQty: number;
    ageDays: number | null;
  };
  stores: ProductInventoryStoreDetail[];
};

type BalanceBucket = { warehouseQty: number; inTransitQty: number; storeById: Map<string, number> };
type RankedInventoryItem = { sku: string; qty: number; ageDays: number | null; inTransitQty?: number };

const EXPORT_MAX_ROWS = 10_000;
const EXPORT_PAGE_SIZE = 500;
const MS_PER_DAY = 24 * 60 * 60 * 1000;

@Injectable()
export class InventoryService {
  constructor(
    @InjectModel(InventoryLedgerEntry.name) private readonly ledgerModel: Model<InventoryLedgerDocument>,
    @InjectModel(Product.name) private readonly productModel: Model<ProductDocument>,
    @InjectModel(Location.name) private readonly locationModel: Model<LocationDocument>,
    @InjectModel(Store.name) private readonly storeModel: Model<StoreDocument>,
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

  async getFilteredInventory(params: FilteredInventoryParams) {
    this.validateRange(params.minQty, params.maxQty, 'quantity');
    this.validateRange(params.minAgeDays, params.maxAgeDays, 'stock age');
    this.validateDateRange(params.fromDate, params.toDate);

    const page = Math.max(1, params.page ?? 1);
    const limit = Math.min(500, Math.max(1, params.limit ?? 200));
    const storeCode = params.storeCode?.trim().toLowerCase();

    const ranked = storeCode
      ? await this.resolveFilteredStoreInventory(storeCode, params)
      : await this.resolveFilteredWarehouseInventory(params);
    const total = ranked.length;
    const pageSlice = ranked.slice((page - 1) * limit, page * limit);
    const productBySku = await this.fetchEnrichedProductMap(pageSlice.map((row) => row.sku));

    const data = pageSlice
      .map((row) => {
        const product = productBySku.get(row.sku);
        if (!product) return null;
        return this.mapFilteredInventoryRow(product, row, storeCode);
      })
      .filter((row): row is FilteredInventoryRow => row != null);

    return {
      inventoryKind: storeCode ? 'store' : 'warehouse',
      ...(storeCode ? { storeCode } : {}),
      data,
      total,
      page,
      limit,
      totalPages: total > 0 ? Math.ceil(total / limit) : 0,
    };
  }

  async getFilteredInventorySummary(
    params: Omit<FilteredInventoryParams, 'page' | 'limit'>,
  ): Promise<FilteredInventorySummary> {
    this.validateRange(params.minQty, params.maxQty, 'quantity');
    this.validateRange(params.minAgeDays, params.maxAgeDays, 'stock age');
    this.validateDateRange(params.fromDate, params.toDate);

    const storeCode = params.storeCode?.trim().toLowerCase();
    const ranked = storeCode
      ? await this.resolveFilteredStoreInventory(storeCode, params)
      : await this.resolveFilteredWarehouseInventory(params);

    const productBySku = await this.fetchFilteredEnrichedProductMap(
      ranked.map((row) => row.sku),
      params,
    );
    const matchingRows = ranked.filter((row) => {
      const product = productBySku.get(row.sku);
      if (!product) return false;
      return this.productMatchesSearch(product, params.search);
    });

    const balanceMap =
      !storeCode && matchingRows.length > 0
        ? await this.aggregateBalancesForSkus(matchingRows.map((row) => row.sku))
        : new Map<string, BalanceBucket>();

    let warehouseQty = 0;
    let inTransitQty = 0;
    let storeQty = 0;
    let sellingAmount = 0;
    let costAmount = 0;
    const supplierIds = new Set<string>();

    for (const row of matchingRows) {
      const product = productBySku.get(row.sku);
      if (!product) continue;

      const sellingPrice = typeof product.sellingPrice === 'number' ? product.sellingPrice : 0;
      const costPrice = typeof product.costPrice === 'number' ? product.costPrice : 0;

      if (storeCode) {
        storeQty += row.qty;
        sellingAmount += row.qty * sellingPrice;
        costAmount += row.qty * costPrice;
        const supplierId = this.extractSupplierId(product);
        if (supplierId) supplierIds.add(supplierId);
      } else {
        warehouseQty += row.qty;
        inTransitQty += row.inTransitQty ?? 0;
        costAmount += row.qty * costPrice;
        const bucket = balanceMap.get(row.sku);
        const rowStoreQty = bucket ? this.sumStoreQty(bucket.storeById) : 0;
        storeQty += rowStoreQty;
        sellingAmount += rowStoreQty * sellingPrice;
        if (rowStoreQty > 0) {
          const supplierId = this.extractSupplierId(product);
          if (supplierId) supplierIds.add(supplierId);
        }
      }
    }

    return {
      inventoryKind: storeCode ? 'store' : 'warehouse',
      ...(storeCode ? { storeCode } : {}),
      filteredSkuCount: matchingRows.length,
      warehouseQty: roundMoney(warehouseQty),
      inTransitQty: roundMoney(inTransitQty),
      storeQty: roundMoney(storeQty),
      supplierCount: supplierIds.size,
      sellingAmount: roundMoney(sellingAmount),
      costAmount: roundMoney(costAmount),
    };
  }

  async getProductInventoryDetail(code: string): Promise<ProductInventoryDetail> {
    const trimmedCode = code?.trim();
    if (!trimmedCode) throw new BadRequestException('code is required');

    const rawProduct = await this.resolveProductByCode(trimmedCode);
    if (!rawProduct) throw new NotFoundException(`Product '${trimmedCode}' not found`);

    const [product] = await enrichProductDocuments(this.productModel, [
      rawProduct as Record<string, unknown>,
    ]);
    if (!product) throw new NotFoundException(`Product '${trimmedCode}' not found`);
    const enrichedProduct: Record<string, unknown> = product;
    const sku = typeof enrichedProduct.sku === 'string' ? enrichedProduct.sku : '';
    if (!sku) throw new NotFoundException(`Product '${trimmedCode}' not found`);

    const [warehouseMaps, warehouseAgeMap, storeQtyMaps, storeAgeMap] = await Promise.all([
      this.getWarehouseSkuQtyMaps(),
      this.getWarehouseStockAgeBySku([sku]),
      this.getAllStoreSkuQtyMaps(),
      this.getStoreStockAgeByStoreForSku(sku),
    ]);

    const storeCodes: string[] = [];
    const storeQtyByCode = new Map<string, number>();
    for (const [storeCode, skuQtyMap] of storeQtyMaps.entries()) {
      const storeQty = skuQtyMap.get(sku) ?? 0;
      if (storeQty <= 0) continue;
      storeCodes.push(storeCode);
      storeQtyByCode.set(storeCode, storeQty);
    }

    const storeNameByCode = await this.getStoreNameByCode(storeCodes);
    const stores = storeCodes
      .sort((a, b) => a.localeCompare(b))
      .map((storeCode) => ({
        storeCode,
        storeName: storeNameByCode.get(storeCode) ?? null,
        storeQty: storeQtyByCode.get(storeCode) ?? 0,
        ageDays: storeAgeMap.get(storeCode) ?? null,
      }));

    return {
      product: enrichedProduct,
      warehouse: {
        warehouseQty: warehouseMaps.warehouseBySku.get(sku) ?? 0,
        inTransitQty: warehouseMaps.inTransitBySku.get(sku) ?? 0,
        ageDays: warehouseAgeMap.get(sku) ?? null,
      },
      stores,
    };
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

  private async resolveFilteredWarehouseInventory(
    params: FilteredInventoryParams,
  ): Promise<RankedInventoryItem[]> {
    const qtyMaps = await this.getWarehouseSkuQtyMaps();
    let ranked: RankedInventoryItem[] = [...qtyMaps.warehouseBySku.entries()]
      .filter(([, qty]) => qty > 0)
      .filter(([, qty]) => this.numberInRange(qty, params.minQty, params.maxQty))
      .map(([sku, qty]) => ({
        sku,
        qty,
        ageDays: null as number | null,
        inTransitQty: qtyMaps.inTransitBySku.get(sku) ?? 0,
      }));

    const oldestInwardBySku = await this.getWarehouseOldestInwardDateBySku(ranked.map((row) => row.sku));
    ranked = this.applyAgeAndDateFilters(ranked, oldestInwardBySku, params);
    return await this.applyProductFiltersAndSort(ranked, params);
  }

  private async resolveFilteredStoreInventory(
    storeCode: string,
    params: FilteredInventoryParams,
  ): Promise<RankedInventoryItem[]> {
    const storeQtyMap = await this.getStoreSkuQtyMap(storeCode);
    let ranked: RankedInventoryItem[] = [...storeQtyMap.entries()]
      .filter(([, qty]) => qty > 0)
      .filter(([, qty]) => this.numberInRange(qty, params.minQty, params.maxQty))
      .map(([sku, qty]) => ({ sku, qty, ageDays: null as number | null }));

    const oldestInwardBySku = await this.getStoreOldestInwardDateBySku(
      ranked.map((row) => row.sku),
      storeCode,
    );
    ranked = this.applyAgeAndDateFilters(ranked, oldestInwardBySku, params);
    return await this.applyProductFiltersAndSort(ranked, params);
  }

  private applyAgeAndDateFilters(
    ranked: RankedInventoryItem[],
    oldestInwardBySku: Map<string, Date>,
    params: Pick<FilteredInventoryParams, 'minAgeDays' | 'maxAgeDays' | 'fromDate' | 'toDate'>,
  ): RankedInventoryItem[] {
    const ageBySku = this.mapAgesFromOldestDates(oldestInwardBySku);
    const fromBoundary = this.parseDateBoundary(params.fromDate, 'start');
    const toBoundary = this.parseDateBoundary(params.toDate, 'end');

    return ranked
      .map((row) => ({ ...row, ageDays: ageBySku.get(row.sku) ?? null }))
      .filter((row) => this.ageInRange(row.ageDays, params.minAgeDays, params.maxAgeDays))
      .filter((row) => {
        if (!fromBoundary && !toBoundary) return true;
        const inwardDate = oldestInwardBySku.get(row.sku);
        if (!inwardDate) return false;
        const time = inwardDate.getTime();
        if (Number.isNaN(time)) return false;
        if (fromBoundary && time < fromBoundary.getTime()) return false;
        if (toBoundary && time > toBoundary.getTime()) return false;
        return true;
      });
  }

  private async applyProductFiltersAndSort(
    ranked: RankedInventoryItem[],
    params: Pick<
      FilteredInventoryParams,
      'search' | 'departmentId' | 'categoryId' | 'subCategoryId' | 'supplierId'
    >,
  ): Promise<RankedInventoryItem[]> {
    if (ranked.length === 0) return [];
    const productBySku = await this.fetchFilteredEnrichedProductMap(ranked.map((row) => row.sku), params);
    const filtered = ranked.filter((row) => {
      const product = productBySku.get(row.sku);
      if (!product) return false;
      return this.productMatchesSearch(product, params.search);
    });
    return filtered.sort((a, b) => b.qty - a.qty || a.sku.localeCompare(b.sku));
  }

  private async fetchFilteredEnrichedProductMap(
    skus: string[],
    params: Pick<
      FilteredInventoryParams,
      'departmentId' | 'categoryId' | 'subCategoryId' | 'supplierId'
    >,
  ): Promise<Map<string, Record<string, unknown>>> {
    const uniqueSkus = [...new Set(skus.map((sku) => sku.trim()).filter(Boolean))];
    const result = new Map<string, Record<string, unknown>>();
    if (uniqueSkus.length === 0) return result;

    const filter: FilterQuery<ProductDocument> = { sku: { $in: uniqueSkus } };
    applyObjectIdRefFilter(filter, 'departmentId', params.departmentId);
    applyObjectIdRefFilter(filter, 'categoryId', params.categoryId);
    applyObjectIdRefFilter(filter, 'subCategoryId', params.subCategoryId);
    applyObjectIdRefFilter(filter, 'supplierNameId', params.supplierId);

    const rawProducts = await this.productModel.find(filter).lean();
    const enriched = await enrichProductDocuments(
      this.productModel,
      rawProducts as Array<Record<string, unknown>>,
    );
    for (const product of enriched) {
      if (typeof product.sku === 'string') result.set(product.sku, product);
    }
    return result;
  }

  private async fetchEnrichedProductMap(skus: string[]): Promise<Map<string, Record<string, unknown>>> {
    const uniqueSkus = [...new Set(skus.map((sku) => sku.trim()).filter(Boolean))];
    const result = new Map<string, Record<string, unknown>>();
    if (uniqueSkus.length === 0) return result;

    const rawProducts = await this.productModel.find({ sku: { $in: uniqueSkus } }).lean();
    const enriched = await enrichProductDocuments(
      this.productModel,
      rawProducts as Array<Record<string, unknown>>,
    );
    for (const product of enriched) {
      if (typeof product.sku === 'string') result.set(product.sku, product);
    }
    return result;
  }

  private mapFilteredInventoryRow(
    product: Record<string, unknown>,
    row: RankedInventoryItem,
    storeCode?: string,
  ): FilteredInventoryRow {
    const storePrice =
      (typeof product.storePrice === 'number' ? product.storePrice : undefined) ??
      (typeof product.sellingPrice === 'number' ? product.sellingPrice : undefined);
    const inventoryKind = storeCode ? 'store' : 'warehouse';
    const result: FilteredInventoryRow = {
      sku: row.sku,
      product,
      inventoryKind,
      ...(storeCode ? { storeCode } : {}),
      stockQty: row.qty,
      warehouseQty: inventoryKind === 'warehouse' ? row.qty : 0,
      inTransitQty: row.inTransitQty ?? 0,
      storeQty: inventoryKind === 'store' ? row.qty : 0,
      stockAgeDays: row.ageDays,
    };
    if (product._id != null) result.productId = String(product._id);
    if (typeof product.upcEanCode === 'string') result.upcEanCode = product.upcEanCode;
    if (typeof product.mrp === 'number') result.mrp = product.mrp;
    if (storePrice !== undefined) result.storePrice = storePrice;
    return result;
  }

  private async getWarehouseStockAgeBySku(skus: string[]): Promise<Map<string, number>> {
    return this.mapAgesFromOldestDates(await this.getWarehouseOldestInwardDateBySku(skus));
  }

  private async getWarehouseOldestInwardDateBySku(skus: string[]): Promise<Map<string, Date>> {
    const uniqueSkus = [...new Set(skus.map((sku) => sku.trim()).filter(Boolean))];
    const result = new Map<string, Date>();
    if (uniqueSkus.length === 0) return result;

    type AgeRow = { _id: string; oldestAt?: Date | string | null };
    const rows = await this.ledgerModel.aggregate<AgeRow>([
      { $match: { sku: { $in: uniqueSkus }, qtyDelta: { $gt: 0 } } },
      { $addFields: { loc: { $ifNull: ['$locationKind', 'warehouse'] } } },
      { $match: { loc: 'warehouse' } },
      { $group: { _id: '$sku', oldestAt: { $min: '$createdAt' } } },
    ]);

    for (const row of rows) {
      const date = this.toValidDate(row.oldestAt);
      if (date) result.set(row._id, date);
    }
    return result;
  }

  private async getStoreStockAgeBySku(skus: string[], storeCode: string): Promise<Map<string, number>> {
    return this.mapAgesFromOldestDates(await this.getStoreOldestInwardDateBySku(skus, storeCode));
  }

  private async getStoreOldestInwardDateBySku(
    skus: string[],
    storeCode: string,
  ): Promise<Map<string, Date>> {
    const uniqueSkus = [...new Set(skus.map((sku) => sku.trim()).filter(Boolean))];
    const normalizedStoreCode = storeCode.trim().toLowerCase();
    const result = new Map<string, Date>();
    if (uniqueSkus.length === 0 || !normalizedStoreCode) return result;

    type AgeRow = { _id: string; oldestAt?: Date | string | null };
    const rows = await this.ledgerModel.aggregate<AgeRow>([
      { $match: { sku: { $in: uniqueSkus }, qtyDelta: { $gt: 0 } } },
      { $addFields: { loc: { $ifNull: ['$locationKind', 'warehouse'] } } },
      { $match: { loc: 'store', storeId: { $exists: true, $nin: [null, ''] } } },
      {
        $addFields: {
          normalizedStoreId: { $toLower: { $trim: { input: { $toString: '$storeId' } } } },
        },
      },
      { $match: { normalizedStoreId: normalizedStoreCode } },
      { $group: { _id: '$sku', oldestAt: { $min: '$createdAt' } } },
    ]);

    for (const row of rows) {
      const date = this.toValidDate(row.oldestAt);
      if (date) result.set(row._id, date);
    }
    return result;
  }

  private async getStoreStockAgeByStoreForSku(sku: string): Promise<Map<string, number>> {
    const trimmedSku = sku.trim();
    const result = new Map<string, number>();
    if (!trimmedSku) return result;

    type AgeRow = { _id: string; oldestAt?: Date | string | null };
    const rows = await this.ledgerModel.aggregate<AgeRow>([
      { $match: { sku: trimmedSku, qtyDelta: { $gt: 0 } } },
      { $addFields: { loc: { $ifNull: ['$locationKind', 'warehouse'] } } },
      { $match: { loc: 'store', storeId: { $exists: true, $nin: [null, ''] } } },
      {
        $addFields: {
          normalizedStoreId: { $toLower: { $trim: { input: { $toString: '$storeId' } } } },
        },
      },
      { $match: { normalizedStoreId: { $nin: [null, ''] } } },
      { $group: { _id: '$normalizedStoreId', oldestAt: { $min: '$createdAt' } } },
    ]);

    for (const row of rows) {
      const ageDays = this.ageDaysFromDate(row.oldestAt);
      if (ageDays !== null) result.set(row._id, ageDays);
    }
    return result;
  }

  private async resolveProductByCode(code: string): Promise<Record<string, unknown> | null> {
    const bySku = await this.productsService.findBySku(code);
    if (bySku) return bySku as Record<string, unknown>;

    const exactBarcode = await this.productModel.findOne({ upcEanCode: code }).lean();
    if (exactBarcode) return exactBarcode as Record<string, unknown>;

    const rx = new RegExp(`^${this.escapeRegex(code)}$`, 'i');
    const caseInsensitive = await this.productModel
      .findOne({ $or: [{ sku: rx }, { upcEanCode: rx }] })
      .lean();
    return (caseInsensitive as Record<string, unknown> | null) ?? null;
  }

  private async getStoreNameByCode(storeCodes: string[]): Promise<Map<string, string>> {
    const uniqueCodes = [...new Set(storeCodes.map((code) => code.trim().toLowerCase()).filter(Boolean))];
    const result = new Map<string, string>();
    if (uniqueCodes.length === 0) return result;

    const stores = await this.storeModel.find({ code: { $in: uniqueCodes } }).select('code name').lean();
    for (const store of stores) {
      if (store.code) result.set(store.code, store.name);
    }
    return result;
  }

  private validateRange(min: number | undefined, max: number | undefined, label: string) {
    if (min !== undefined && max !== undefined && min > max) {
      throw new BadRequestException(`min ${label} cannot be greater than max ${label}`);
    }
  }

  private validateDateRange(fromDate?: string, toDate?: string) {
    if (!fromDate || !toDate) return;
    const from = this.parseDateBoundary(fromDate, 'start');
    const to = this.parseDateBoundary(toDate, 'end');
    if (!from || !to) throw new BadRequestException('Invalid date range');
    if (from.getTime() > to.getTime()) {
      throw new BadRequestException('fromDate cannot be greater than toDate');
    }
  }

  private numberInRange(value: number, min?: number, max?: number): boolean {
    if (min !== undefined && value < min) return false;
    if (max !== undefined && value > max) return false;
    return true;
  }

  private ageInRange(ageDays: number | null, min?: number, max?: number): boolean {
    if (min === undefined && max === undefined) return true;
    if (ageDays === null) return false;
    return this.numberInRange(ageDays, min, max);
  }

  private ageDaysFromDate(value: Date | string | null | undefined): number | null {
    const date = this.toValidDate(value);
    if (!date) return null;
    const time = date.getTime();
    if (Number.isNaN(time)) return null;
    return Math.max(0, Math.floor((Date.now() - time) / MS_PER_DAY));
  }

  private toValidDate(value: Date | string | null | undefined): Date | null {
    if (value == null) return null;
    const date = value instanceof Date ? value : new Date(value);
    const time = date.getTime();
    return Number.isNaN(time) ? null : date;
  }

  private mapAgesFromOldestDates(oldestBySku: Map<string, Date>): Map<string, number> {
    const result = new Map<string, number>();
    for (const [sku, date] of oldestBySku.entries()) {
      const age = this.ageDaysFromDate(date);
      if (age !== null) result.set(sku, age);
    }
    return result;
  }

  private parseDateBoundary(value: string | undefined, boundary: 'start' | 'end'): Date | null {
    const trimmed = value?.trim();
    if (!trimmed) return null;
    const isoDateOnly = /^\d{4}-\d{2}-\d{2}$/;
    const normalized =
      boundary === 'start'
        ? (isoDateOnly.test(trimmed) ? `${trimmed}T00:00:00.000Z` : trimmed)
        : (isoDateOnly.test(trimmed) ? `${trimmed}T23:59:59.999Z` : trimmed);
    return this.toValidDate(normalized);
  }

  private extractSupplierId(product: Record<string, unknown>): string | undefined {
    return coerceObjectIdString(product.supplierNameId);
  }

  private productMatchesSearch(product: Record<string, unknown>, search?: string): boolean {
    const needle = search?.trim().toLowerCase();
    if (!needle) return true;
    return ['itemName', 'shortName', 'alias', 'sku', 'upcEanCode'].some((field) => {
      const value = product[field];
      return typeof value === 'string' && value.toLowerCase().includes(needle);
    });
  }

  private escapeRegex(value: string): string {
    return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
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
