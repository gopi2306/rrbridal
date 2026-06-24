import { BadRequestException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { enrichProductDocuments } from '../../common/product-line-enrichment';
import { InventoryService } from '../inventory/inventory.service';
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

type TallyLineInput = {
  sku: string;
  scannedQty?: number;
  qty?: number;
};

@Injectable()
export class StockTallyService {
  constructor(
    private readonly inventoryService: InventoryService,
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
    const data = await this.mapRankedToRows(tally.storeId, pageSlice);

    const skuCount = ranked.length;
    const totalScannedQty = ranked.reduce((sum, line) => sum + line.scannedQty, 0);

    return {
      storeCode: tally.storeId,
      tallyId: String(tally._id),
      tallyNo: tally.tallyNo,
      status: tally.status,
      skuCount,
      totalScannedQty,
      totalQty: totalScannedQty,
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

  async replaceLines(
    storeCode: string,
    lines: TallyLineInput[],
  ): Promise<StockTallySessionResponse> {
    const tally = await this.getOrCreateOpenTally(storeCode);
    tally.lines = this.normalizeLineInputs(lines);
    if (tally.lines.length === 0) {
      throw new BadRequestException('At least one line with quantity greater than zero is required');
    }

    tally.markModified('lines');
    await tally.save();

    return this.getSession(storeCode, { page: 1, limit: 200 });
  }

  async save(
    storeCode: string,
    lineInputs?: TallyLineInput[],
  ): Promise<StockTallySaveResponse> {
    const tally = await this.getOrCreateOpenTally(storeCode);

    if (lineInputs !== undefined) {
      tally.lines = this.normalizeLineInputs(lineInputs);
      tally.markModified('lines');
      await tally.save();
    }

    if (tally.lines.length === 0) {
      throw new BadRequestException('Cannot save an empty tally session');
    }

    const scans = tally.lines
      .filter((line) => line.scannedQty > 0)
      .map((line) => ({
        sku: line.sku,
        scannedQty: line.scannedQty,
      }));

    if (scans.length === 0) {
      throw new BadRequestException('Cannot save an empty tally session');
    }

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
      lines: scans.map((line) => ({
        sku: line.sku,
        scannedQty: line.scannedQty,
        qty: line.scannedQty,
      })),
    };
  }

  private normalizeLineInputs(lines: TallyLineInput[]): StockTallyLine[] {
    const bySku = new Map<string, number>();
    for (const line of lines) {
      const sku = line.sku?.trim() ?? '';
      if (!sku) continue;
      const qty = Math.max(0, line.scannedQty ?? line.qty ?? 0);
      if (qty <= 0) continue;
      bySku.set(sku, qty);
    }

    return [...bySku.entries()].map(([sku, scannedQty]) => ({ sku, scannedQty }));
  }

  private async consolidateOpenTallies(storeId: string): Promise<StockTallyDocument | null> {
    const drafts = await this.tallyModel
      .find({ storeId, status: OPEN_TALLY_STATUS })
      .sort({ updatedAt: -1 })
      .exec();

    if (drafts.length === 0) return null;

    const primary = drafts[0];
    if (!primary) return null;

    if (drafts.length === 1) return primary;

    const merged = new Map<string, number>();
    for (const draft of drafts) {
      for (const line of draft.lines) {
        if (line.scannedQty <= 0) continue;
        merged.set(line.sku, (merged.get(line.sku) ?? 0) + line.scannedQty);
      }
    }

    primary.lines = [...merged.entries()].map(([sku, scannedQty]) => ({ sku, scannedQty }));
    primary.markModified('lines');
    await primary.save();

    const extraIds = drafts.slice(1).map((draft) => draft._id);
    await this.tallyModel.deleteMany({ _id: { $in: extraIds } });

    return primary;
  }

  private async getOrCreateOpenTally(storeCode: string): Promise<StockTallyDocument> {
    const sid = await this.resolveStoreCode(storeCode);

    const consolidated = await this.consolidateOpenTallies(sid);
    if (consolidated) return consolidated;

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

  private async mapRankedToRows(
    storeId: string,
    ranked: RankedTallyLine[],
  ): Promise<StockTallyLineRow[]> {
    if (ranked.length === 0) return [];

    const skus = ranked.map((line) => line.sku);
    const [products, inventoryQtyMap] = await Promise.all([
      this.productModel.find({ sku: { $in: skus } }).lean(),
      this.inventoryService.getStoreQtyBySkus(storeId, skus),
    ]);
    const enriched = await enrichProductDocuments(
      this.productModel,
      products as Array<Record<string, unknown>>,
    );
    const bySku = new Map(enriched.map((p) => [typeof p.sku === 'string' ? p.sku : '', p]));

    return ranked.map((line) =>
      this.mapLineRow(bySku.get(line.sku) ?? {}, line, inventoryQtyMap.get(line.sku) ?? 0),
    );
  }

  private mapLineRow(
    product: Record<string, unknown>,
    line: RankedTallyLine,
    orderedQty: number,
  ): StockTallyLineRow {
    const storePrice =
      (typeof product.storePrice === 'number' ? product.storePrice : undefined) ??
      (typeof product.sellingPrice === 'number' ? product.sellingPrice : undefined) ??
      null;
    return {
      sku: line.sku,
      productName: typeof product.itemName === 'string' ? product.itemName : line.sku,
      productSubtitle: this.productSubtitle(product),
      barcode: typeof product.upcEanCode === 'string' ? product.upcEanCode : null,
      orderedQty,
      storeQty: orderedQty,
      scannedQty: line.scannedQty,
      qty: line.scannedQty,
      gstPercent: this.resolveGstPercent(product),
      costPrice: typeof product.costPrice === 'number' ? product.costPrice : null,
      mrp: typeof product.mrp === 'number' ? product.mrp : null,
      sellingPrice: typeof product.sellingPrice === 'number' ? product.sellingPrice : null,
      storePrice,
    };
  }

  private resolveGstPercent(product: Record<string, unknown>): number | null {
    if (typeof product.gstPercent === 'number') return product.gstPercent;
    const hsn = product.hsnCodeId as { gstPercent?: number } | undefined;
    if (hsn && typeof hsn.gstPercent === 'number') return hsn.gstPercent;
    return null;
  }

  private productSubtitle(product: Record<string, unknown>): string {
    const category = product.categoryId as { name?: string } | undefined;
    const subCategory = product.subCategoryId as { name?: string } | undefined;
    const parts = [category?.name, subCategory?.name].filter((v): v is string => Boolean(v?.trim()));
    return parts.join(' - ');
  }
}
