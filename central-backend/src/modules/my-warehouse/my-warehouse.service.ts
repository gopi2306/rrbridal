import { Injectable, NotFoundException, PayloadTooLargeException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model, Types } from 'mongoose';
import { enrichProductDocuments } from '../../common/product-line-enrichment';
import { TABULAR_EXPORT_MAX_ROWS } from '../../common/tabular-export';
import {
  GoodsReceipt,
  GoodsReceiptDocument,
  GoodsReceiptLineOutcome,
} from '../goods-receipts/schemas/goods-receipt.schema';
import { InventoryService, WarehouseSkuQtyScope } from '../inventory/inventory.service';
import { Location, LocationDocument } from '../locations/schemas/location.schema';
import { Product, ProductDocument } from '../products/schemas/product.schema';
import { ProductsService } from '../products/products.service';
import {
  PurchaseIntent,
  PurchaseIntentDocument,
} from '../purchase-intents/schemas/purchase-intent.schema';
import {
  PurchaseOrder,
  PurchaseOrderDocument,
} from '../purchase-orders/schemas/purchase-order.schema';
import { StockTransfer, StockTransferDocument } from '../stock-transfers/schemas/stock-transfer.schema';
import { Store, StoreDocument } from '../stores/schemas/store.schema';
import type {
  MyWarehouseGoodsReceipt,
  MyWarehouseInventoryGridRow,
  MyWarehouseInventoryListParams,
  MyWarehouseInventoryListResponse,
  MyWarehouseInventoryPreviewRow,
  MyWarehouseInventorySummary,
  MyWarehouseProfile,
  MyWarehousePurchaseOrder,
  MyWarehouseQueryLimits,
  MyWarehouseTransferOut,
  MyWarehouseWorkspaceResponse,
} from './my-warehouse.types';

const OPEN_PO_STATUSES = ['open', 'awaiting_approval', 'approved', 'partially_received'] as const;

type ResolvedWarehouse = {
  profile: MyWarehouseProfile;
  locationId: string;
  legacyDefaultLocationId: string;
  qtyScope: WarehouseSkuQtyScope;
};

type GrLean = {
  _id: Types.ObjectId;
  receiptNo: string;
  grnNumber?: string;
  poNo?: string;
  supplier?: { name?: string };
  status: string;
  lines?: Array<{ receivedQty?: number; outcome?: GoodsReceiptLineOutcome }>;
  updatedAt?: Date;
  createdAt?: Date;
};

type PoLean = {
  _id: Types.ObjectId;
  poNo: string;
  supplier?: { name?: string; shortname?: string };
  deliveryDate?: string;
  status: string;
  lines?: Array<{ recdQty?: number; freeQty?: number }>;
  updatedAt?: Date;
  createdAt?: Date;
};

type TransferLean = {
  _id: Types.ObjectId;
  transferNo: string;
  status: string;
  transferDate?: string;
  stockClassification?: string;
  toStoreId?: string;
  purchaseIntentId?: Types.ObjectId;
  lines?: Array<{ qty?: number }>;
  updatedAt?: Date;
  createdAt?: Date;
};

@Injectable()
export class MyWarehouseService {
  constructor(
    private readonly inventoryService: InventoryService,
    private readonly productsService: ProductsService,
    @InjectModel(Location.name) private readonly locationModel: Model<LocationDocument>,
    @InjectModel(GoodsReceipt.name) private readonly grModel: Model<GoodsReceiptDocument>,
    @InjectModel(PurchaseOrder.name) private readonly poModel: Model<PurchaseOrderDocument>,
    @InjectModel(StockTransfer.name) private readonly transferModel: Model<StockTransferDocument>,
    @InjectModel(PurchaseIntent.name) private readonly intentModel: Model<PurchaseIntentDocument>,
    @InjectModel(Store.name) private readonly storeModel: Model<StoreDocument>,
    @InjectModel(Product.name) private readonly productModel: Model<ProductDocument>,
  ) {}

  async listWarehouseInventory(
    locationCode: string,
    params: MyWarehouseInventoryListParams,
  ): Promise<MyWarehouseInventoryListResponse> {
    const page = Math.max(1, params.page);
    const limit = Math.min(100, Math.max(1, params.limit));
    const { locationCode: code, ranked, inTransitBySku } = await this.resolveWarehouseInventoryRanked(
      locationCode,
      params.search,
    );

    const total = ranked.length;
    const pageSlice = ranked.slice((page - 1) * limit, page * limit);
    const data = await this.mapRankedToWarehouseGridRows(pageSlice, inTransitBySku);

    return {
      locationCode: code,
      data,
      total,
      page,
      limit,
      totalPages: total > 0 ? Math.ceil(total / limit) : 0,
    };
  }

  async fetchAllWarehouseInventoryRows(
    locationCode: string,
    search?: string,
    maxRows = TABULAR_EXPORT_MAX_ROWS,
  ): Promise<MyWarehouseInventoryGridRow[]> {
    const { ranked, inTransitBySku } = await this.resolveWarehouseInventoryRanked(locationCode, search);
    if (ranked.length > maxRows) {
      throw new PayloadTooLargeException(
        `Export exceeds maximum of ${maxRows} rows (${ranked.length} match). Narrow search and try again.`,
      );
    }
    return this.mapRankedToWarehouseGridRows(ranked, inTransitBySku);
  }

  private async resolveWarehouseInventoryRanked(locationCode: string, search?: string) {
    const resolved = await this.resolveWarehouse(locationCode);
    const qtyMaps = await this.inventoryService.getWarehouseSkuQtyMaps(resolved.qtyScope);

    let ranked = [...qtyMaps.warehouseBySku.entries()]
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

    return {
      locationCode: resolved.profile.code,
      ranked,
      inTransitBySku: qtyMaps.inTransitBySku,
    };
  }

  private async mapRankedToWarehouseGridRows(
    ranked: Array<[string, number]>,
    inTransitBySku: Map<string, number>,
  ): Promise<MyWarehouseInventoryGridRow[]> {
    if (ranked.length === 0) return [];

    const skus = ranked.map(([sku]) => sku);
    const products = await this.productModel.find({ sku: { $in: skus } }).lean();
    const enriched = await enrichProductDocuments(
      this.productModel,
      products as Array<Record<string, unknown>>,
    );
    const bySku = new Map(enriched.map((p) => [typeof p.sku === 'string' ? p.sku : '', p]));

    return ranked.map(([sku, warehouseQty]) =>
      this.mapInventoryGridRow(bySku.get(sku) ?? {}, sku, warehouseQty, inTransitBySku.get(sku) ?? 0),
    );
  }

  async getWorkspace(
    locationCode: string,
    limits: MyWarehouseQueryLimits,
  ): Promise<MyWarehouseWorkspaceResponse> {
    const resolved = await this.resolveWarehouse(locationCode);
    const qtyMaps = await this.inventoryService.getWarehouseSkuQtyMaps(resolved.qtyScope);

    const [goodsReceipts, purchaseOrders, transfersOut, inventoryPreview, inventorySummary] =
      await Promise.all([
        this.listGoodsReceipts(limits.goodsReceiptLimit),
        this.listPurchaseOrders(limits.purchaseOrderLimit),
        this.listTransfersOut(resolved, limits.transferOutLimit),
        this.buildInventoryPreview(qtyMaps, limits.inventoryPreviewLimit),
        this.buildInventorySummary(qtyMaps),
      ]);

    return {
      warehouse: resolved.profile,
      inventorySummary,
      goodsReceipts,
      purchaseOrders,
      transfersOut,
      inventoryPreview,
    };
  }

  private async resolveWarehouse(locationCode: string): Promise<ResolvedWarehouse> {
    const defaultLoc = await this.locationModel
      .findOne({ isActive: true, type: 'warehouse' })
      .sort({ code: 1 })
      .lean();

    const code = locationCode.trim().toLowerCase();
    const loc = await this.locationModel
      .findOne({ code, isActive: true, type: 'warehouse' })
      .lean();

    if (!loc) {
      throw new NotFoundException(`Warehouse location '${locationCode}' not found`);
    }

    const legacyDefaultLocationCode = defaultLoc ? defaultLoc.code : loc.code;

    return {
      profile: {
        code: loc.code,
        name: loc.name,
        address: loc.address?.trim() || null,
        phone: null,
        type: loc.type?.trim() || 'warehouse',
        updatedAt: this.toIso(this.docTimestamp(loc as Record<string, unknown>)),
      },
      locationId: String(loc._id),
      legacyDefaultLocationId: defaultLoc ? String(defaultLoc._id) : String(loc._id),
      qtyScope: {
        locationCode: loc.code,
        legacyDefaultLocationCode,
      },
    };
  }

  private async buildInventorySummary(qtyMaps: {
    stockUnits: number;
    inTransitUnits: number;
    warehouseBySku: Map<string, number>;
  }): Promise<MyWarehouseInventorySummary> {
    const stockValue = await this.computeStockValue(qtyMaps.warehouseBySku);
    return {
      warehouseQty: qtyMaps.stockUnits,
      inTransitQty: qtyMaps.inTransitUnits,
      stockValue,
    };
  }

  private async computeStockValue(warehouseBySku: Map<string, number>): Promise<number> {
    const skus = [...warehouseBySku.entries()].filter(([, q]) => q > 0).map(([sku]) => sku);
    if (skus.length === 0) return 0;

    const products = await this.productModel
      .find({ sku: { $in: skus }, isActive: true })
      .select('sku costPrice')
      .lean();

    let total = 0;
    for (const p of products) {
      const qty = warehouseBySku.get(p.sku) ?? 0;
      if (qty <= 0) continue;
      const cost = typeof p.costPrice === 'number' && Number.isFinite(p.costPrice) ? p.costPrice : 0;
      total += qty * cost;
    }
    return total;
  }

  private async listGoodsReceipts(limit: number): Promise<MyWarehouseGoodsReceipt[]> {
    const receipts = await this.grModel
      .find()
      .sort({ updatedAt: -1, createdAt: -1 })
      .limit(limit)
      .lean<GrLean[]>();

    return receipts.map((gr) => {
      const lineStats = this.summarizeGrLines(gr.lines);
      const supplierName = gr.supplier?.name?.trim() || null;
      const poNo = gr.poNo?.trim() || null;
      const grn = gr.grnNumber?.trim() || null;

      return {
        id: String(gr._id),
        receiptNo: gr.receiptNo,
        grnNumber: grn,
        mrcNumber: grn,
        poNo,
        supplierName,
        reference: poNo && supplierName ? `${poNo} - ${supplierName}` : poNo ?? supplierName,
        lineCount: lineStats.lineCount,
        validQty: lineStats.validQty,
        damagedCount: lineStats.damagedCount,
        summary: lineStats.summary,
        status: gr.status as MyWarehouseGoodsReceipt['status'],
        statusLabel: this.formatStatusLabel(gr.status),
        updatedAt: this.toIso(this.docTimestamp(gr as Record<string, unknown>)),
      };
    });
  }

  private async listPurchaseOrders(limit: number): Promise<MyWarehousePurchaseOrder[]> {
    const orders = await this.poModel
      .find({ status: { $in: [...OPEN_PO_STATUSES] } })
      .sort({ deliveryDate: 1, updatedAt: -1 })
      .limit(limit)
      .lean<PoLean[]>();

    return orders.map((po) => {
      const totalPieces = this.sumPoPieces(po.lines);
      const supplierName = po.supplier?.name ?? po.supplier?.shortname ?? 'Supplier';
      const deliveryDate = po.deliveryDate ?? null;
      const summary = deliveryDate ? `${deliveryDate} - ${totalPieces} pcs` : `${totalPieces} pcs`;

      return {
        id: String(po._id),
        poNo: po.poNo,
        supplierName,
        deliveryDate,
        totalPieces,
        summary,
        status: po.status as MyWarehousePurchaseOrder['status'],
        statusLabel: this.formatPoStatusLabel(po.status),
        updatedAt: this.toIso(this.docTimestamp(po as Record<string, unknown>)),
      };
    });
  }

  private async listTransfersOut(
    resolved: ResolvedWarehouse,
    limit: number,
  ): Promise<MyWarehouseTransferOut[]> {
    const transfers = await this.transferModel
      .find(this.transferFilterForWarehouse(resolved))
      .sort({ transferDate: -1, updatedAt: -1 })
      .limit(limit)
      .lean<TransferLean[]>();

    const storeCodes = [
      ...new Set(transfers.map((t) => t.toStoreId?.trim().toLowerCase()).filter(Boolean)),
    ] as string[];
    const storeNameByCode = new Map<string, string>();
    if (storeCodes.length > 0) {
      const stores = await this.storeModel.find({ code: { $in: storeCodes } }).select('code name').lean();
      for (const store of stores) {
        storeNameByCode.set(store.code, store.name);
      }
    }

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
      const toStoreId = transfer.toStoreId?.trim().toLowerCase() ?? null;
      return {
        id: String(transfer._id),
        transferNo: transfer.transferNo,
        status: transfer.status as MyWarehouseTransferOut['status'],
        statusLabel: this.formatStatusLabel(transfer.status),
        date: transfer.transferDate ?? null,
        classificationTag: transfer.stockClassification?.trim() || null,
        toStoreId,
        toStoreName: toStoreId ? (storeNameByCode.get(toStoreId) ?? toStoreId) : null,
        purchaseIntentNo: transfer.purchaseIntentId
          ? (intentNoById.get(String(transfer.purchaseIntentId)) ?? null)
          : null,
        lineCount: transfer.lines?.length ?? 0,
        totalPieces: this.sumTransferQty(transfer.lines),
        updatedAt: this.toIso(this.docTimestamp(transfer as Record<string, unknown>)),
      };
    });
  }

  private transferFilterForWarehouse(resolved: ResolvedWarehouse): Record<string, unknown> {
    return {
      direction: 'warehouse_to_store',
      ...this.locationMatchOrLegacy(
        resolved.locationId,
        resolved.legacyDefaultLocationId,
        'fromLocationId',
      ),
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

  private async buildInventoryPreview(
    qtyMaps: {
      warehouseBySku: Map<string, number>;
      inTransitBySku: Map<string, number>;
    },
    limit: number,
  ): Promise<MyWarehouseInventoryPreviewRow[]> {
    const ranked = [...qtyMaps.warehouseBySku.entries()]
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

    return ranked.map(([sku, warehouseQty]) =>
      this.mapInventoryGridRow(
        bySku.get(sku) ?? {},
        sku,
        warehouseQty,
        qtyMaps.inTransitBySku.get(sku) ?? 0,
      ),
    );
  }

  private mapInventoryGridRow(
    product: Record<string, unknown>,
    sku: string,
    warehouseQty: number,
    inTransitQty: number,
  ): MyWarehouseInventoryGridRow {
    const sellingPrice =
      (typeof product.sellingPrice === 'number' ? product.sellingPrice : undefined) ??
      (typeof product.mrp === 'number' ? product.mrp : undefined) ??
      null;
    return {
      sku,
      productName: typeof product.itemName === 'string' ? product.itemName : sku,
      productSubtitle: this.productSubtitle(product),
      barcode: typeof product.upcEanCode === 'string' ? product.upcEanCode : null,
      warehouseQty,
      inTransitQty,
      costPrice: typeof product.costPrice === 'number' ? product.costPrice : null,
      sellingPrice,
    };
  }

  private summarizeGrLines(
    lines: Array<{ receivedQty?: number; outcome?: GoodsReceiptLineOutcome }> | undefined,
  ) {
    const lineCount = lines?.length ?? 0;
    let validQty = 0;
    let damagedCount = 0;

    for (const line of lines ?? []) {
      const outcome = line.outcome ?? 'valid';
      if (outcome === 'damaged' || outcome === 'partially-damaged') {
        damagedCount += 1;
      } else if (outcome !== 'invalid') {
        validQty += line.receivedQty ?? 0;
      }
    }

    const parts = [`${lineCount} lines`, `${validQty} valid`];
    if (damagedCount > 0) parts.push(`${damagedCount} damaged`);

    return { lineCount, validQty, damagedCount, summary: parts.join(' - ') };
  }

  private sumTransferQty(lines: Array<{ qty?: number }> | undefined): number {
    if (!lines?.length) return 0;
    return lines.reduce((sum, line) => sum + (line.qty ?? 0), 0);
  }

  private sumPoPieces(lines: Array<{ recdQty?: number; freeQty?: number }> | undefined): number {
    if (!lines?.length) return 0;
    return lines.reduce((sum, line) => {
      const ordered = (line.recdQty ?? 0) + (line.freeQty ?? 0);
      return sum + (ordered > 0 ? ordered : 1);
    }, 0);
  }

  private productSubtitle(product: Record<string, unknown>): string {
    const brand = product.brandId as { name?: string } | undefined;
    const category = product.categoryId as { name?: string } | undefined;
    const parts = [brand?.name, category?.name].filter((v): v is string => Boolean(v?.trim()));
    return parts.join(' - ');
  }

  private formatStatusLabel(status: string): string {
    return status
      .split('_')
      .map((part) => (part ? part.charAt(0).toUpperCase() + part.slice(1).toLowerCase() : part))
      .join(' ');
  }

  private formatPoStatusLabel(status: string): string {
    if (status === 'open') return 'Submitted';
    return this.formatStatusLabel(status);
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
