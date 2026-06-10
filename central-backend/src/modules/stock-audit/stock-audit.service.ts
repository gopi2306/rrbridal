import { Injectable, NotFoundException, PayloadTooLargeException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { enrichProductDocuments } from '../../common/product-line-enrichment';
import { TABULAR_EXPORT_MAX_ROWS } from '../../common/tabular-export';
import { InventoryService } from '../inventory/inventory.service';
import { Product, ProductDocument } from '../products/schemas/product.schema';
import { ProductsService } from '../products/products.service';
import { Store, StoreDocument } from '../stores/schemas/store.schema';
import {
  StockAudit,
  StockAuditDocument,
  StockAuditLine,
  StockAuditStatus,
} from './schemas/stock-audit.schema';
import type { StockAuditLineRow, StockAuditListParams, StockAuditListResponse } from './stock-audit.types';

const OPEN_AUDIT_STATUSES: StockAuditStatus[] = ['draft', 'in_progress'];

type RankedAuditLine = {
  sku: string;
  orderedQty: number;
  scannedQty: number;
};

@Injectable()
export class StockAuditService {
  constructor(
    private readonly inventoryService: InventoryService,
    private readonly productsService: ProductsService,
    @InjectModel(StockAudit.name) private readonly auditModel: Model<StockAuditDocument>,
    @InjectModel(Store.name) private readonly storeModel: Model<StoreDocument>,
    @InjectModel(Product.name) private readonly productModel: Model<ProductDocument>,
  ) {}

  async listAuditLines(storeCode: string, params: StockAuditListParams): Promise<StockAuditListResponse> {
    const page = Math.max(1, params.page);
    const limit = Math.min(100, Math.max(1, params.limit));
    const audit = await this.getOrCreateOpenAudit(storeCode);
    const ranked = await this.resolveRankedLines(audit.lines, params.search);

    const total = ranked.length;
    const pageSlice = ranked.slice((page - 1) * limit, page * limit);
    const data = await this.mapRankedToRows(pageSlice);

    return {
      storeCode: audit.storeId,
      auditId: String(audit._id),
      auditNo: audit.auditNo,
      status: audit.status,
      data,
      total,
      page,
      limit,
      totalPages: total > 0 ? Math.ceil(total / limit) : 0,
    };
  }

  async applyScannedCounts(
    storeCode: string,
    scans: Array<{ sku: string; scannedQty: number }>,
  ): Promise<StockAuditDocument> {
    const sid = await this.resolveStoreCode(storeCode);
    const audit = await this.getOrCreateOpenAudit(storeCode);
    const storeQtyMap = await this.inventoryService.getStoreSkuQtyMap(sid);
    const lineBySku = new Map(audit.lines.map((line) => [line.sku, line]));

    for (const scan of scans) {
      const sku = scan.sku.trim();
      if (!sku) continue;
      const scannedQty = Math.max(0, scan.scannedQty);
      const existing = lineBySku.get(sku);
      if (existing) {
        existing.scannedQty = scannedQty;
      } else {
        const line: StockAuditLine = { sku, orderedQty: storeQtyMap.get(sku) ?? 0, scannedQty };
        audit.lines.push(line);
        lineBySku.set(sku, line);
      }
    }

    audit.markModified('lines');
    return await audit.save();
  }

  async fetchAllAuditLines(
    storeCode: string,
    search?: string,
    maxRows = TABULAR_EXPORT_MAX_ROWS,
  ): Promise<{ audit: StockAuditDocument; rows: StockAuditLineRow[] }> {
    const audit = await this.getOrCreateOpenAudit(storeCode);
    const ranked = await this.resolveRankedLines(audit.lines, search);
    if (ranked.length > maxRows) {
      throw new PayloadTooLargeException(
        `Export exceeds maximum of ${maxRows} rows (${ranked.length} match). Narrow search and try again.`,
      );
    }
    const rows = await this.mapRankedToRows(ranked);
    return { audit, rows };
  }

  private async getOrCreateOpenAudit(storeCode: string): Promise<StockAuditDocument> {
    const sid = await this.resolveStoreCode(storeCode);

    const existing = await this.auditModel
      .findOne({ storeId: sid, status: { $in: OPEN_AUDIT_STATUSES } })
      .sort({ updatedAt: -1 })
      .exec();

    if (existing) return existing;

    const storeQtyMap = await this.inventoryService.getStoreSkuQtyMap(sid);
    const lines: StockAuditLine[] = [...storeQtyMap.entries()]
      .filter(([, qty]) => qty > 0)
      .sort((a, b) => b[1] - a[1])
      .map(([sku, orderedQty]) => ({
        sku,
        orderedQty,
        scannedQty: 0,
      }));

    const auditNo = await this.allocateAuditNo();
    return await this.auditModel.create({
      auditNo,
      storeId: sid,
      status: 'in_progress',
      lines,
    });
  }

  private async resolveStoreCode(storeCode: string): Promise<string> {
    const code = storeCode.trim().toLowerCase();
    const store = await this.storeModel.findOne({ code, status: 'active' }).lean();
    if (!store) {
      throw new NotFoundException(`Store '${storeCode}' not found or inactive`);
    }
    return store.code;
  }

  private async allocateAuditNo(): Promise<string> {
    const count = await this.auditModel.countDocuments();
    return `SA-${String(count + 1).padStart(6, '0')}`;
  }

  private async resolveRankedLines(
    lines: StockAuditLine[],
    search?: string,
  ): Promise<RankedAuditLine[]> {
    let ranked = lines
      .filter((line) => line.orderedQty > 0 || line.scannedQty > 0)
      .map((line) => ({
        sku: line.sku,
        orderedQty: line.orderedQty,
        scannedQty: line.scannedQty,
      }))
      .sort((a, b) => b.orderedQty - a.orderedQty);

    const trimmedSearch = search?.trim();
    if (trimmedSearch && ranked.length > 0) {
      const skus = ranked.map((line) => line.sku);
      const matching = await this.productsService.list({
        skus,
        search: trimmedSearch,
        skip: 0,
        limit: skus.length,
      });
      const matchSet = new Set(
        matching.map((p) => (typeof p.sku === 'string' ? p.sku : '')).filter(Boolean),
      );
      ranked = ranked.filter((line) => matchSet.has(line.sku));
    }

    return ranked;
  }

  private async mapRankedToRows(ranked: RankedAuditLine[]): Promise<StockAuditLineRow[]> {
    if (ranked.length === 0) return [];

    const skus = ranked.map((line) => line.sku);
    const products = await this.productModel.find({ sku: { $in: skus } }).lean();
    const enriched = await enrichProductDocuments(
      this.productModel,
      products as Array<Record<string, unknown>>,
    );
    const bySku = new Map(enriched.map((p) => [typeof p.sku === 'string' ? p.sku : '', p]));

    return ranked.map((line) => this.mapLineRow(bySku.get(line.sku) ?? {}, line));
  }

  private mapLineRow(
    product: Record<string, unknown>,
    line: RankedAuditLine,
  ): StockAuditLineRow {
    const storePrice =
      (typeof product.storePrice === 'number' ? product.storePrice : undefined) ??
      (typeof product.sellingPrice === 'number' ? product.sellingPrice : undefined) ??
      null;
    return {
      sku: line.sku,
      productName: typeof product.itemName === 'string' ? product.itemName : line.sku,
      productSubtitle: this.productSubtitle(product),
      orderedQty: line.orderedQty,
      scannedQty: line.scannedQty,
      varianceQty: line.scannedQty - line.orderedQty,
      gstPercent: typeof product.gstPercent === 'number' ? product.gstPercent : null,
      costPrice: typeof product.costPrice === 'number' ? product.costPrice : null,
      mrp: typeof product.mrp === 'number' ? product.mrp : null,
      sellingPrice: typeof product.sellingPrice === 'number' ? product.sellingPrice : null,
      storePrice,
    };
  }

  private productSubtitle(product: Record<string, unknown>): string {
    const category = product.categoryId as { name?: string } | undefined;
    const subCategory = product.subCategoryId as { name?: string } | undefined;
    const parts = [category?.name, subCategory?.name].filter((v): v is string => Boolean(v?.trim()));
    return parts.join(' - ');
  }
}
