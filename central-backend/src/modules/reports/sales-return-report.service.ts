import { BadRequestException, Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import type { Model } from 'mongoose';
import { enrichProductDocuments } from '../../common/product-line-enrichment';
import { roundMoney } from '../../common/money.util';
import { resolveDashboardStore } from '../dashboard/dashboard-store.util';
import {
  buildStoreSalePayloadTimeFilter,
  formatBusinessYmd,
  parseOccurredAt,
  readString,
  resolveBillsListDateRange,
} from '../dashboard/store-sales-payload.util';
import { Product, ProductDocument } from '../products/schemas/product.schema';
import { Store, StoreDocument } from '../stores/schemas/store.schema';
import { StoreInvoice, StoreInvoiceDocument } from '../store-sales/schemas/store-invoice.schema';
import { StoreSaleReturn, StoreSaleReturnDocument } from '../store-sales/schemas/store-sale-return.schema';
import { SalesReturnReportQueryDto } from './dto/sales-return-report-query.dto';
import {
  formatLegacyDateTime,
  parseReturnReportLines,
  productCategoryFields,
} from './sales-return-report.util';
import type {
  SalesReturnReportResponse,
  SalesReturnReportRow,
  SalesReturnReportTotals,
} from './sales-return-report.types';

@Injectable()
export class SalesReturnReportService {
  constructor(
    @InjectModel(Store.name) private readonly storeModel: Model<StoreDocument>,
    @InjectModel(Product.name) private readonly productModel: Model<ProductDocument>,
    @InjectModel(StoreSaleReturn.name) private readonly returnModel: Model<StoreSaleReturnDocument>,
    @InjectModel(StoreInvoice.name) private readonly invoiceModel: Model<StoreInvoiceDocument>,
  ) {}

  async buildReport(query: SalesReturnReportQueryDto): Promise<SalesReturnReportResponse> {
    const store = await resolveDashboardStore(this.storeModel, query.storeCode);
    const range = this.resolveRange(query);
    const filter = buildStoreSalePayloadTimeFilter(store.code, range);

    const docs = await this.returnModel.find(filter).sort({ createdAt: 1 }).lean();
    const posCounter = query.posCounter?.trim();

    const candidateRows: Array<{
      doc: (typeof docs)[number];
      payload: Record<string, unknown>;
      line: ReturnType<typeof parseReturnReportLines>[number];
      returnDate: string;
      returnTime: string;
      billNo: string;
      msrNo: string;
      customerName: string;
      returnCounter: string;
    }> = [];

    const skus = new Set<string>();
    const billNos = new Set<string>();

    for (const doc of docs) {
      const payload = (doc.payload ?? {}) as Record<string, unknown>;
      if (posCounter) {
        const counter = readString(payload.posCounter) ?? '';
        if (counter !== posCounter) continue;
      }

      const lines = parseReturnReportLines(payload);
      if (lines.length === 0) continue;

      const occurred = parseOccurredAt(payload, (doc as { createdAt?: Date }).createdAt);
      const returnDate = occurred ? formatBusinessYmd(occurred) : '';
      const returnTime = formatLegacyDateTime(payload.createdAtUtc, occurred ?? undefined);
      const billNo = readString(payload.originalBillNo) ?? readString(payload.billNo) ?? '';
      const customerName = readString(payload.customerName) ?? '';
      const returnCounter = readString(payload.posCounter) ?? '';

      if (billNo) billNos.add(billNo);
      for (const line of lines) {
        if (line.sku) skus.add(line.sku);
        candidateRows.push({
          doc,
          payload,
          line,
          returnDate,
          returnTime,
          billNo,
          msrNo: doc.returnNo,
          customerName,
          returnCounter,
        });
      }
    }

    const [productBySku, billTimeByNo] = await Promise.all([
      this.loadProductsBySku([...skus]),
      this.loadBillTimes(store.code, [...billNos]),
    ]);

    const allRows: SalesReturnReportRow[] = candidateRows.map((entry) => {
      const product = entry.line.sku ? productBySku.get(entry.line.sku) : undefined;
      const categories = productCategoryFields(product);
      return {
        ...categories,
        returnDate: entry.returnDate,
        billNo: entry.billNo,
        msrNo: entry.msrNo,
        customerName: entry.customerName,
        itemName: entry.line.itemName,
        qty: entry.line.qty,
        selling: entry.line.selling,
        mrp: entry.line.mrp,
        taxPercent: entry.line.taxPercent,
        taxAmount: entry.line.taxAmount,
        returnAmount: entry.line.returnAmount,
        returnCounter: entry.returnCounter,
        billTime: billTimeByNo.get(entry.billNo) ?? '',
        returnTime: entry.returnTime,
        sku: entry.line.sku,
        isLegacy: entry.payload.isLegacy === true,
        originalBillDate: readString(entry.payload.originalBillDate) ?? '',
      };
    });

    const limit = query.limit ?? 10000;
    const truncated = allRows.length > limit;
    const data = truncated ? allRows.slice(0, limit) : allRows;

    return {
      period: {
        from: query.from,
        to: query.to,
        storeCode: store.code,
        storeName: store.name,
        ...(posCounter ? { posCounter } : {}),
      },
      truncated,
      total: allRows.length,
      totals: this.summarizeRows(data),
      data,
    };
  }

  private summarizeRows(rows: SalesReturnReportRow[]): SalesReturnReportTotals {
    return rows.reduce<SalesReturnReportTotals>(
      (acc, row) => ({
        qty: roundMoney(acc.qty + row.qty),
        taxAmount: roundMoney(acc.taxAmount + row.taxAmount),
        returnAmount: roundMoney(acc.returnAmount + row.returnAmount),
      }),
      { qty: 0, taxAmount: 0, returnAmount: 0 },
    );
  }

  private async loadProductsBySku(skus: string[]): Promise<Map<string, Record<string, unknown>>> {
    if (skus.length === 0) return new Map();
    const docs = await this.productModel.find({ sku: { $in: skus } }).lean();
    const enriched = await enrichProductDocuments(
      this.productModel,
      docs as Array<Record<string, unknown>>,
    );
    const map = new Map<string, Record<string, unknown>>();
    for (const doc of enriched) {
      const sku = typeof doc.sku === 'string' ? doc.sku : '';
      if (sku) map.set(sku, doc);
    }
    return map;
  }

  private async loadBillTimes(storeId: string, billNos: string[]): Promise<Map<string, string>> {
    if (billNos.length === 0) return new Map();
    const invoices = await this.invoiceModel
      .find({ storeId, invoiceNo: { $in: billNos } })
      .select({ invoiceNo: 1, payload: 1, createdAt: 1 })
      .lean();

    const map = new Map<string, string>();
    for (const inv of invoices) {
      const payload = (inv.payload ?? {}) as Record<string, unknown>;
      const occurred = parseOccurredAt(payload, (inv as { createdAt?: Date }).createdAt);
      const formatted = formatLegacyDateTime(payload.createdAtUtc, occurred ?? undefined);
      map.set(inv.invoiceNo, formatted);
    }
    return map;
  }

  private resolveRange(query: SalesReturnReportQueryDto) {
    try {
      return resolveBillsListDateRange(query.from, query.to);
    } catch (err: unknown) {
      throw new BadRequestException(err instanceof Error ? err.message : String(err));
    }
  }
}
