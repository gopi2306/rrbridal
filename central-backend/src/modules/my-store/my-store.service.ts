import { Injectable, NotFoundException, PayloadTooLargeException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model, Types } from 'mongoose';
import { enrichProductDocuments } from '../../common/product-line-enrichment';
import { TABULAR_EXPORT_MAX_ROWS } from '../../common/tabular-export';
import { InventoryService } from '../inventory/inventory.service';
import { Product, ProductDocument } from '../products/schemas/product.schema';
import { ProductsService } from '../products/products.service';
import {
  PurchaseIntent,
  PurchaseIntentDocument,
} from '../purchase-intents/schemas/purchase-intent.schema';
import { StockTransfer, StockTransferDocument } from '../stock-transfers/schemas/stock-transfer.schema';
import { Store, StoreDocument } from '../stores/schemas/store.schema';
import type {
  MyStoreInventoryPreviewRow,
  MyStoreInventorySummary,
  MyStoreProfile,
  MyStorePurchaseIndent,
  MyStoreQueryLimits,
  MyStoreInventoryGridRow,
  MyStoreInventoryListParams,
  MyStoreInventoryListResponse,
  MyStoreTransferCard,
  MyStoreWorkspaceResponse,
} from './my-store.types';

const INBOUND_TRANSFER_STATUSES = ['draft', 'in_transit', 'awaiting_intake'] as const;

type TransferLean = {
  _id: Types.ObjectId;
  transferNo: string;
  status: string;
  transferDate?: string;
  direction: string;
  purchaseIntentId?: Types.ObjectId;
  lines?: Array<{ sku?: string; qty?: number }>;
  updatedAt?: Date;
  createdAt?: Date;
};

type IntentLean = {
  _id: Types.ObjectId;
  intentNo: string;
  status: string;
  remarks?: string;
  lines?: Array<{ requestedQty?: number }>;
  updatedAt?: Date;
  createdAt?: Date;
};

@Injectable()
export class MyStoreService {
  constructor(
    private readonly inventoryService: InventoryService,
    private readonly productsService: ProductsService,
    @InjectModel(Store.name) private readonly storeModel: Model<StoreDocument>,
    @InjectModel(PurchaseIntent.name) private readonly intentModel: Model<PurchaseIntentDocument>,
    @InjectModel(StockTransfer.name) private readonly transferModel: Model<StockTransferDocument>,
    @InjectModel(Product.name) private readonly productModel: Model<ProductDocument>,
  ) {}

  async listStoreInventory(
    storeCode: string,
    params: MyStoreInventoryListParams,
  ): Promise<MyStoreInventoryListResponse> {
    const page = Math.max(1, params.page);
    const limit = Math.min(100, Math.max(1, params.limit));
    const { storeCode: sid, ranked, inboundInTransitBySku } = await this.resolveStoreInventoryRanked(
      storeCode,
      params.search,
    );

    const total = ranked.length;
    const pageSlice = ranked.slice((page - 1) * limit, page * limit);
    const data = await this.mapRankedToStoreGridRows(pageSlice, inboundInTransitBySku);

    return {
      storeCode: sid,
      data,
      total,
      page,
      limit,
      totalPages: total > 0 ? Math.ceil(total / limit) : 0,
    };
  }

  async fetchAllStoreInventoryRows(
    storeCode: string,
    search?: string,
    maxRows = TABULAR_EXPORT_MAX_ROWS,
  ): Promise<MyStoreInventoryGridRow[]> {
    const { ranked, inboundInTransitBySku } = await this.resolveStoreInventoryRanked(storeCode, search);
    if (ranked.length > maxRows) {
      throw new PayloadTooLargeException(
        `Export exceeds maximum of ${maxRows} rows (${ranked.length} match). Narrow search and try again.`,
      );
    }
    return this.mapRankedToStoreGridRows(ranked, inboundInTransitBySku);
  }

  private async resolveStoreInventoryRanked(storeCode: string, search?: string) {
    const storeDoc = await this.resolveStore(storeCode);
    const sid = storeDoc.code;

    const [storeQtyMap, inboundInTransitBySku] = await Promise.all([
      this.inventoryService.getStoreSkuQtyMap(sid),
      this.getInboundInTransitBySku(sid),
    ]);

    let ranked = [...storeQtyMap.entries()]
      .filter(([, qty]) => qty > 0)
      .sort((a, b) => b[1] - a[1]);

    const trimmedSearch = search?.trim();
    if (trimmedSearch && ranked.length > 0) {
      const stockSkus = ranked.map(([sku]) => sku);
      const matching = await this.productsService.list({
        skus: stockSkus,
        search: trimmedSearch,
        skip: 0,
        limit: stockSkus.length,
      });
      const matchSet = new Set(
        matching.map((p) => (typeof p.sku === 'string' ? p.sku : '')).filter(Boolean),
      );
      ranked = ranked.filter(([sku]) => matchSet.has(sku));
    }

    return { storeCode: sid, ranked, inboundInTransitBySku };
  }

  private async mapRankedToStoreGridRows(
    ranked: Array<[string, number]>,
    inboundInTransitBySku: Map<string, number>,
  ): Promise<MyStoreInventoryGridRow[]> {
    if (ranked.length === 0) return [];

    const skus = ranked.map(([sku]) => sku);
    const products = await this.productModel.find({ sku: { $in: skus } }).lean();
    const enriched = await enrichProductDocuments(
      this.productModel,
      products as Array<Record<string, unknown>>,
    );
    const bySku = new Map(enriched.map((p) => [typeof p.sku === 'string' ? p.sku : '', p]));

    return ranked.map(([sku, storeQty]) =>
      this.mapInventoryGridRow(bySku.get(sku) ?? {}, sku, storeQty, inboundInTransitBySku.get(sku) ?? 0),
    );
  }

  async getWorkspace(
    storeId: string | undefined,
    limits: MyStoreQueryLimits,
  ): Promise<MyStoreWorkspaceResponse> {
    const storeDoc = await this.resolveStore(storeId);
    const sid = storeDoc.code;

    const [warehouseMaps, storeQtyMap, inboundInTransitBySku] = await Promise.all([
      this.inventoryService.getWarehouseSkuQtyMaps(),
      this.inventoryService.getStoreSkuQtyMap(sid),
      this.getInboundInTransitBySku(sid),
    ]);

    const [
      purchaseIndents,
      transfersIn,
      transfersOut,
      inventoryPreview,
      inventorySummary,
    ] = await Promise.all([
      this.listPurchaseIndents(sid, limits.purchaseIndentLimit),
      this.listTransfersIn(sid, limits.transferInLimit),
      this.listTransfersOut(sid, limits.transferOutLimit),
      this.buildInventoryPreview(storeQtyMap, inboundInTransitBySku, limits.inventoryPreviewLimit),
      this.buildInventorySummary(warehouseMaps.stockUnits, storeQtyMap, inboundInTransitBySku),
    ]);

    return {
      store: this.mapStoreProfile(storeDoc),
      inventorySummary,
      purchaseIndents,
      transfersIn,
      transfersOut,
      inventoryPreview,
    };
  }

  private async resolveStore(storeId: string | undefined) {
    const stores = await this.storeModel.find({ status: 'active' }).sort({ code: 1 }).lean();
    const code = storeId?.trim().toLowerCase();
    const match = code ? stores.find((s) => s.code === code) : stores[0];

    if (!match) {
      if (code) throw new NotFoundException(`Store '${storeId}' not found or inactive`);
      throw new NotFoundException('No active stores configured');
    }

    return match;
  }

  private mapStoreProfile(doc: {
    code: string;
    name: string;
    address?: string;
    phone?: string;
    status: 'active' | 'inactive';
    updatedAt?: Date;
    createdAt?: Date;
  }): MyStoreProfile {
    return {
      code: doc.code,
      name: doc.name,
      address: doc.address?.trim() || null,
      phone: doc.phone?.trim() || null,
      status: doc.status,
      updatedAt: this.toIso(this.docTimestamp(doc as Record<string, unknown>)),
    };
  }

  private async buildInventorySummary(
    warehouseQty: number,
    storeQtyMap: Map<string, number>,
    inboundInTransitBySku: Map<string, number>,
  ): Promise<MyStoreInventorySummary> {
    let storeQty = 0;
    for (const qty of storeQtyMap.values()) {
      if (qty > 0) storeQty += qty;
    }

    let inTransitQty = 0;
    for (const qty of inboundInTransitBySku.values()) {
      if (qty > 0) inTransitQty += qty;
    }

    const retailValue = await this.computeRetailValue(storeQtyMap);

    return { warehouseQty, storeQty, inTransitQty, retailValue };
  }

  private async computeRetailValue(storeQtyMap: Map<string, number>): Promise<number> {
    const skus = [...storeQtyMap.entries()].filter(([, q]) => q > 0).map(([sku]) => sku);
    if (skus.length === 0) return 0;

    const products = await this.productModel
      .find({ sku: { $in: skus }, isActive: true })
      .select('sku storePrice sellingPrice')
      .lean();

    let total = 0;
    for (const p of products) {
      const qty = storeQtyMap.get(p.sku) ?? 0;
      if (qty <= 0) continue;
      const price =
        (typeof p.storePrice === 'number' ? p.storePrice : undefined) ??
        (typeof p.sellingPrice === 'number' ? p.sellingPrice : undefined) ??
        0;
      total += qty * price;
    }
    return total;
  }

  private async listPurchaseIndents(storeId: string, limit: number): Promise<MyStorePurchaseIndent[]> {
    const intents = await this.intentModel
      .find({ storeId })
      .sort({ updatedAt: -1, createdAt: -1 })
      .limit(limit)
      .lean<IntentLean[]>();

    return intents.map((intent) => {
      const lineCount = intent.lines?.length ?? 0;
      const requestedQty = this.sumIntentQty(intent.lines);
      return {
        id: String(intent._id),
        intentNo: intent.intentNo,
        status: intent.status as MyStorePurchaseIndent['status'],
        statusLabel: this.formatStatusLabel(intent.status),
        lineCount,
        requestedQty,
        summary: `${lineCount} lines - ${requestedQty} requested`,
        description: intent.remarks?.trim() || null,
        updatedAt: this.toIso(this.docTimestamp(intent as Record<string, unknown>)),
      };
    });
  }

  private async listTransfersIn(storeId: string, limit: number): Promise<MyStoreTransferCard[]> {
    const transfers = await this.transferModel
      .find({
        direction: 'warehouse_to_store',
        toStoreId: storeId,
        status: { $in: [...INBOUND_TRANSFER_STATUSES] },
      })
      .sort({ transferDate: -1, updatedAt: -1 })
      .limit(limit)
      .lean<TransferLean[]>();

    return this.mapTransferCards(transfers, 'warehouse to store');
  }

  private async listTransfersOut(storeId: string, limit: number): Promise<MyStoreTransferCard[]> {
    const transfers = await this.transferModel
      .find({
        direction: 'store_to_warehouse',
        fromStoreId: storeId,
      })
      .sort({ transferDate: -1, updatedAt: -1 })
      .limit(limit)
      .lean<TransferLean[]>();

    return this.mapTransferCards(transfers, 'store to warehouse');
  }

  private async mapTransferCards(
    transfers: TransferLean[],
    directionLabel: string,
  ): Promise<MyStoreTransferCard[]> {
    const intentIds = transfers
      .map((t) => t.purchaseIntentId)
      .filter((id): id is Types.ObjectId => id != null);

    const intentNoById = new Map<string, string>();
    if (intentIds.length > 0) {
      const intents = await this.intentModel
        .find({ _id: { $in: intentIds } })
        .select('intentNo')
        .lean();
      for (const intent of intents) {
        intentNoById.set(String(intent._id), intent.intentNo);
      }
    }

    return transfers.map((transfer) => {
      const lineCount = transfer.lines?.length ?? 0;
      const totalPieces = this.sumTransferQty(transfer.lines);
      const purchaseIntentNo = transfer.purchaseIntentId
        ? (intentNoById.get(String(transfer.purchaseIntentId)) ?? null)
        : null;
      const summaryParts = [`${lineCount} lines`, `${totalPieces} pcs`];
      if (purchaseIntentNo) summaryParts.push(purchaseIntentNo);

      return {
        id: String(transfer._id),
        transferNo: transfer.transferNo,
        status: transfer.status as MyStoreTransferCard['status'],
        statusLabel: this.formatStatusLabel(transfer.status),
        date: transfer.transferDate ?? null,
        directionLabel,
        lineCount,
        totalPieces,
        summary: summaryParts.join(' - '),
        purchaseIntentNo,
        updatedAt: this.toIso(this.docTimestamp(transfer as Record<string, unknown>)),
      };
    });
  }

  private async buildInventoryPreview(
    storeQtyMap: Map<string, number>,
    inboundInTransitBySku: Map<string, number>,
    limit: number,
  ): Promise<MyStoreInventoryPreviewRow[]> {
    const ranked = [...storeQtyMap.entries()]
      .filter(([, qty]) => qty > 0)
      .sort((a, b) => b[1] - a[1])
      .slice(0, limit);

    if (ranked.length === 0) return [];

    const skus = ranked.map(([sku]) => sku);
    const products = await this.productModel.find({ sku: { $in: skus } }).lean();
    const enriched = await enrichProductDocuments(
      this.productModel,
      products as Array<Record<string, unknown>>,
    );
    const bySku = new Map(enriched.map((p) => [typeof p.sku === 'string' ? p.sku : '', p]));

    return ranked.map(([sku, storeQty]) =>
      this.mapInventoryGridRow(
        bySku.get(sku) ?? {},
        sku,
        storeQty,
        inboundInTransitBySku.get(sku) ?? 0,
      ),
    );
  }

  private mapInventoryGridRow(
    product: Record<string, unknown>,
    sku: string,
    storeQty: number,
    inTransitQty: number,
  ): MyStoreInventoryGridRow {
    const storePrice =
      (typeof product.storePrice === 'number' ? product.storePrice : undefined) ??
      (typeof product.sellingPrice === 'number' ? product.sellingPrice : undefined) ??
      null;
    return {
      sku,
      productName: typeof product.itemName === 'string' ? product.itemName : sku,
      productSubtitle: this.productSubtitle(product),
      barcode: typeof product.upcEanCode === 'string' ? product.upcEanCode : null,
      storeQty,
      inTransitQty,
      mrp: typeof product.mrp === 'number' ? product.mrp : null,
      storePrice,
    };
  }

  private async getInboundInTransitBySku(storeId: string): Promise<Map<string, number>> {
    const transfers = await this.transferModel
      .find({
        direction: 'warehouse_to_store',
        toStoreId: storeId,
        status: { $in: [...INBOUND_TRANSFER_STATUSES] },
      })
      .select('lines')
      .lean();

    const map = new Map<string, number>();
    for (const transfer of transfers) {
      for (const line of transfer.lines ?? []) {
        const sku = line.sku?.trim();
        if (!sku) continue;
        const qty = line.qty ?? 0;
        if (qty <= 0) continue;
        map.set(sku, (map.get(sku) ?? 0) + qty);
      }
    }
    return map;
  }

  private productSubtitle(product: Record<string, unknown>): string {
    const brand = product.brandId as { name?: string } | undefined;
    const category = product.categoryId as { name?: string } | undefined;
    const parts = [brand?.name, category?.name].filter((v): v is string => Boolean(v?.trim()));
    return parts.join(' - ');
  }

  private sumTransferQty(lines: Array<{ qty?: number }> | undefined): number {
    if (!lines?.length) return 0;
    return lines.reduce((sum, line) => sum + (line.qty ?? 0), 0);
  }

  private sumIntentQty(lines: Array<{ requestedQty?: number }> | undefined): number {
    if (!lines?.length) return 0;
    return lines.reduce((sum, line) => sum + (line.requestedQty ?? 0), 0);
  }

  private formatStatusLabel(status: string): string {
    return status
      .split('_')
      .map((part) => (part ? part.charAt(0).toUpperCase() + part.slice(1).toLowerCase() : part))
      .join(' ');
  }

  private docTimestamp(doc: Record<string, unknown>): unknown {
    return doc.updatedAt ?? doc.createdAt;
  }

  private toIso(value: unknown): string | null {
    if (value == null) return null;
    if (value instanceof Date) return value.toISOString();
    if (typeof value === 'string' || typeof value === 'number') {
      const d = new Date(value);
      if (!Number.isNaN(d.getTime())) return d.toISOString();
    }
    return null;
  }
}
