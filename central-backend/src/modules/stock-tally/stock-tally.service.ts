import { BadRequestException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { enrichProductDocuments } from '../../common/product-line-enrichment';
import { Product, ProductDocument } from '../products/schemas/product.schema';
import { ProductsService } from '../products/products.service';
import { StockAuditService } from '../stock-audit/stock-audit.service';
import { Store, StoreDocument } from '../stores/schemas/store.schema';
import {
  StockTally,
  StockTallyDocument,
  StockTallyLine,
  StockTallyStatus,
} from './schemas/stock-tally.schema';
import type {
  StockTallyLineRow,
  StockTallyListParams,
  StockTallySaveResponse,
  StockTallySessionResponse,
} from './stock-tally.types';

const OPEN_TALLY_STATUS: StockTallyStatus = 'draft';

type RankedTallyLine = {
  sku: string;
  scannedQty: number;
};

@Injectable()
export class StockTallyService {
  constructor(
    private readonly productsService: ProductsService,
    private readonly stockAuditService: StockAuditService,
    @InjectModel(StockTally.name) private readonly tallyModel: Model<StockTallyDocument>,
    @InjectModel(Store.name) private readonly storeModel: Model<StoreDocument>,
    @InjectModel(Product.name) private readonly productModel: Model<ProductDocument>,
  ) {}

  async getSession(storeCode: string, params: StockTallyListParams): Promise<StockTallySessionResponse> {
    const page = Math.max(1, params.page);
    const limit = Math.min(200, Math.max(1, params.limit));
    const tally = await this.getOrCreateOpenTally(storeCode);
    const ranked = await this.resolveRankedLines(tally.lines, params.search);

    const total = ranked.length;
    const pageSlice = ranked.slice((page - 1) * limit, page * limit);
    const data = await this.mapRankedToRows(pageSlice);

    const skuCount = ranked.length;
    const totalScannedQty = ranked.reduce((sum, line) => sum + line.scannedQty, 0);

    return {
      storeCode: tally.storeId,
      tallyId: String(tally._id),
      tallyNo: tally.tallyNo,
      status: tally.status,
      skuCount,
      totalScannedQty,
      data,
      total,
      page,
      limit,
      totalPages: total > 0 ? Math.ceil(total / limit) : 0,
    };
  }

  async scan(storeCode: string, barcodeOrSku: string, qtyDelta = 1): Promise<StockTallySessionResponse> {
    const tally = await this.getOrCreateOpenTally(storeCode);
    const product = await this.findProductByBarcodeOrSku(barcodeOrSku);
    const sku = product.sku;
    const delta = Math.max(1, qtyDelta);

    const line = tally.lines.find((l) => l.sku === sku);
    if (line) {
      line.scannedQty += delta;
    } else {
      tally.lines.push({ sku, scannedQty: delta });
    }

    tally.markModified('lines');
    await tally.save();

    return this.getSession(storeCode, { page: 1, limit: 200 });
  }

  async updateLine(
    storeCode: string,
    sku: string,
    scannedQty: number,
  ): Promise<StockTallySessionResponse> {
    const tally = await this.getOrCreateOpenTally(storeCode);
    const trimmedSku = sku.trim();
    const line = tally.lines.find((l) => l.sku === trimmedSku);

    if (!line) {
      throw new NotFoundException(`SKU '${trimmedSku}' is not in the current tally session`);
    }

    if (scannedQty <= 0) {
      tally.lines = tally.lines.filter((l) => l.sku !== trimmedSku);
    } else {
      line.scannedQty = scannedQty;
    }

    tally.markModified('lines');
    await tally.save();

    return this.getSession(storeCode, { page: 1, limit: 200 });
  }

  async save(storeCode: string): Promise<StockTallySaveResponse> {
    const tally = await this.getOrCreateOpenTally(storeCode);
    if (tally.lines.length === 0) {
      throw new BadRequestException('Cannot save an empty tally session');
    }

    const scans = tally.lines.map((line) => ({
      sku: line.sku,
      scannedQty: line.scannedQty,
    }));

    const audit = await this.stockAuditService.applyScannedCounts(storeCode, scans);

    tally.status = 'saved';
    await tally.save();

    await this.tallyModel.create({
      tallyNo: await this.allocateTallyNo(),
      storeId: tally.storeId,
      status: 'draft',
      lines: [],
    });

    return {
      storeCode: tally.storeId,
      tallyId: String(tally._id),
      tallyNo: tally.tallyNo,
      auditId: String(audit._id),
      auditNo: audit.auditNo,
      linesSaved: scans.length,
      savedAt: new Date().toISOString(),
    };
  }

  private async getOrCreateOpenTally(storeCode: string): Promise<StockTallyDocument> {
    const sid = await this.resolveStoreCode(storeCode);

    const existing = await this.tallyModel
      .findOne({ storeId: sid, status: OPEN_TALLY_STATUS })
      .sort({ updatedAt: -1 })
      .exec();

    if (existing) return existing;

    return await this.tallyModel.create({
      tallyNo: await this.allocateTallyNo(),
      storeId: sid,
      status: OPEN_TALLY_STATUS,
      lines: [],
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

  private async allocateTallyNo(): Promise<string> {
    const count = await this.tallyModel.countDocuments();
    return `ST-${String(count + 1).padStart(6, '0')}`;
  }

  private async findProductByBarcodeOrSku(value: string) {
    const trimmed = value.trim();
    if (!trimmed) {
      throw new BadRequestException('barcodeOrSku is required');
    }

    const byBarcode = await this.productModel.findOne({ upcEanCode: trimmed }).lean();
    if (byBarcode) return byBarcode;

    const bySku = await this.productsService.findBySku(trimmed);
    if (bySku) return bySku;

    throw new NotFoundException(`No product found for barcode or SKU '${trimmed}'`);
  }

  private async resolveRankedLines(
    lines: StockTallyLine[],
    search?: string,
  ): Promise<RankedTallyLine[]> {
    let ranked = lines
      .filter((line) => line.scannedQty > 0)
      .map((line) => ({ sku: line.sku, scannedQty: line.scannedQty }))
      .sort((a, b) => b.scannedQty - a.scannedQty);

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

  private async mapRankedToRows(ranked: RankedTallyLine[]): Promise<StockTallyLineRow[]> {
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

  private mapLineRow(product: Record<string, unknown>, line: RankedTallyLine): StockTallyLineRow {
    const storePrice =
      (typeof product.storePrice === 'number' ? product.storePrice : undefined) ??
      (typeof product.sellingPrice === 'number' ? product.sellingPrice : undefined) ??
      null;
    return {
      sku: line.sku,
      productName: typeof product.itemName === 'string' ? product.itemName : line.sku,
      productSubtitle: this.productSubtitle(product),
      barcode: typeof product.upcEanCode === 'string' ? product.upcEanCode : null,
      scannedQty: line.scannedQty,
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
