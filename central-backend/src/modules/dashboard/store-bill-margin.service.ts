import { BadRequestException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import {
  buildExportFilename,
  buildMultiSheetExcelBuffer,
  formatExportMarginPercent,
  formatExportMoney,
} from '../../common/tabular-export';
import { roundMoney } from '../../common/money.util';
import { Product, ProductDocument } from '../products/schemas/product.schema';
import { StoreAdjustment, StoreAdjustmentDocument } from '../store-sales/schemas/store-adjustment.schema';
import { StoreInvoice, StoreInvoiceDocument } from '../store-sales/schemas/store-invoice.schema';
import { StoreSaleReturn, StoreSaleReturnDocument } from '../store-sales/schemas/store-sale-return.schema';
import { Store, StoreDocument } from '../stores/schemas/store.schema';
import {
  applyAdjustmentOverrides,
  aggregateReturnExGst,
  computeMarginPercentage,
  netTotals,
  parseBillExGstLines,
  sumLines,
} from './store-bill-margin-math.util';
import type {
  StoreBillMarginExportResult,
  StoreBillMarginOptions,
  StoreBillMarginResponse,
  StoreBillMarginRow,
  StoreBillMarginSummary,
} from './store-bill-margin.types';
import {
  buildDashboardPeriod,
  buildStoreSalePayloadTimeFilter,
  parseOccurredAt,
  readString,
  resolveDateRange,
} from './store-sales-payload.util';

const DEFAULT_LIMIT = 5000;
const XLSX_CONTENT_TYPE =
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

const BILL_HEADERS = [
  'Bill no',
  'Posted',
  'Bill date',
  'Counter',
  'Customer',
  'Salesman code',
  'Salesman name',
  'Qty',
  'Cost (ex GST)',
  'Selling (ex GST)',
  'Discount (ex GST)',
  'Margin %',
  'Margin amt (ex GST)',
  'Returned',
  'Return no',
  'Adjusted',
  'Adjustment no',
] as const;

@Injectable()
export class StoreBillMarginService {
  constructor(
    @InjectModel(StoreInvoice.name) private readonly invoiceModel: Model<StoreInvoiceDocument>,
    @InjectModel(StoreSaleReturn.name) private readonly returnModel: Model<StoreSaleReturnDocument>,
    @InjectModel(StoreAdjustment.name)
    private readonly adjustmentModel: Model<StoreAdjustmentDocument>,
    @InjectModel(Store.name) private readonly storeModel: Model<StoreDocument>,
    @InjectModel(Product.name) private readonly productModel: Model<ProductDocument>,
  ) {}

  async getBillMargin(options: StoreBillMarginOptions): Promise<StoreBillMarginResponse> {
    const store = await this.resolveStore(options.storeId);
    let range;
    try {
      range = resolveDateRange({
        period: options.period,
        ...(options.from !== undefined ? { from: options.from } : {}),
        ...(options.to !== undefined ? { to: options.to } : {}),
        year: options.year,
        month: options.month,
      });
    } catch (err: unknown) {
      throw new BadRequestException(err instanceof Error ? err.message : String(err));
    }

    const limit = Math.min(Math.max(options.limit ?? DEFAULT_LIMIT, 1), DEFAULT_LIMIT);
    const invoices = await this.invoiceModel
      .find(buildStoreSalePayloadTimeFilter(store.code, range))
      .lean();

    const filtered = invoices
      .filter((doc) => this.matchesPosCounter(doc, options.posCounter))
      .filter((doc) => this.matchesSalesman(doc, options.salesmanId))
      .sort((a, b) => {
        const aAt = parseOccurredAt(a.payload ?? {}, docCreatedAt(a))?.getTime() ?? 0;
        const bAt = parseOccurredAt(b.payload ?? {}, docCreatedAt(b))?.getTime() ?? 0;
        return bAt - aAt;
      });

    const totalMatched = filtered.length;
    const truncated = filtered.slice(0, limit);
    const billNos = truncated
      .map((d) => readString(d.payload?.billNo) ?? d.invoiceNo)
      .filter((n): n is string => !!n && n.trim() !== '');

    const [returns, adjustments, costBySku] = await Promise.all([
      this.loadReturnsByBillNos(store.code, billNos),
      this.loadAdjustmentsByBillNos(store.code, billNos),
      this.loadProductCostBySku(truncated),
    ]);

    const rows: StoreBillMarginRow[] = truncated.map((doc) =>
      this.mapRow(doc, returns, adjustments, costBySku),
    );

    return {
      store,
      period: buildDashboardPeriod(options.period, range),
      summary: this.buildSummary(rows),
      rows,
      totalMatched,
      wasTruncated: totalMatched > limit,
    };
  }

  async buildExport(options: StoreBillMarginOptions): Promise<StoreBillMarginExportResult> {
    const report = await this.getBillMargin(options);
    const summaryRows: string[][] = [
      ['Store', `${report.store.code} — ${report.store.name}`],
      ['Period', report.period.label],
      ['From', report.period.from],
      ['To', report.period.to],
      ['Bills', String(report.summary.billCount)],
      ['Total qty', String(report.summary.totalQty)],
      ['Cost (ex GST)', formatExportMoney(report.summary.totalCost)],
      ['Selling (ex GST)', formatExportMoney(report.summary.totalSelling)],
      ['Discount (ex GST)', formatExportMoney(report.summary.totalDiscount)],
      ['Margin amt (ex GST)', formatExportMoney(report.summary.totalMargin)],
      [
        'Margin %',
        formatExportMarginPercent(report.summary.totalMargin, report.summary.totalCost),
      ],
    ];

    const detailRows = report.rows.map((row) => [
      row.billNo,
      row.postedAt,
      row.billDate,
      row.posCounter ?? '',
      row.customerName ?? '',
      row.salesmanCode ?? '',
      row.salesmanName ?? '',
      String(row.qty),
      formatExportMoney(row.costPrice),
      formatExportMoney(row.sellingPrice),
      formatExportMoney(row.discount),
      formatExportMoney(row.marginPercentage),
      formatExportMoney(row.marginAmount),
      row.hasReturn ? 'Yes' : '',
      row.returnNo ?? '',
      row.hasAdjustment ? 'Yes' : '',
      row.adjustmentNo ?? '',
    ]);

    const buffer = buildMultiSheetExcelBuffer([
      { name: 'SUMMARY', headers: ['Label', 'Value'], rows: summaryRows },
      { name: 'BILLS', headers: [...BILL_HEADERS], rows: detailRows },
    ]);

    return {
      filename: buildExportFilename('bill-margin', report.store.code, 'xlsx'),
      contentType: XLSX_CONTENT_TYPE,
      buffer,
    };
  }

  private buildSummary(rows: readonly StoreBillMarginRow[]): StoreBillMarginSummary {
    const totalCost = roundMoney(rows.reduce((s, r) => s + r.costPrice, 0));
    const totalSelling = roundMoney(rows.reduce((s, r) => s + r.sellingPrice, 0));
    const totalDiscount = roundMoney(rows.reduce((s, r) => s + r.discount, 0));
    const totalMargin = roundMoney(rows.reduce((s, r) => s + r.marginAmount, 0));
    const totalQty = roundMoney(rows.reduce((s, r) => s + r.qty, 0));
    return {
      billCount: rows.length,
      totalQty,
      totalCost,
      totalSelling,
      totalDiscount,
      totalMargin,
      marginPercentage: computeMarginPercentage(totalMargin, totalCost),
    };
  }

  private mapRow(
    doc: {
      invoiceNo: string;
      posCounter?: string;
      payload: Record<string, unknown>;
    },
    returns: Map<string, { returnNo: string; payload: Record<string, unknown> }>,
    adjustments: Map<string, { adjustmentNo: string; payload: Record<string, unknown> }>,
    costBySku: ReadonlyMap<string, number>,
  ): StoreBillMarginRow {
    const payload = doc.payload ?? {};
    const billNo = readString(payload.billNo) ?? doc.invoiceNo;
    const occurredAt = parseOccurredAt(payload, docCreatedAt(doc));

    const originalLines = parseBillExGstLines(payload.lines, costBySku);
    const adj = adjustments.get(billNo);
    const effectiveLines = adj
      ? applyAdjustmentOverrides(originalLines, adj.payload.lines)
      : originalLines;
    let totals = sumLines(effectiveLines);

    const ret = returns.get(billNo);
    if (ret) {
      const returnLines = ret.payload.returnLines ?? ret.payload.lines;
      const returnTotals = aggregateReturnExGst(returnLines, originalLines);
      totals = netTotals(totals, returnTotals);
    }

    const marginAmount = roundMoney(totals.selling - totals.cost);
    const marginPercentage = computeMarginPercentage(marginAmount, totals.cost);

    return {
      billNo,
      postedAt: occurredAt ? occurredAt.toISOString() : '',
      billDate: readString(payload.billDate) ?? '',
      posCounter: readString(payload.posCounter) ?? doc.posCounter ?? null,
      customerName: readString(payload.customerName) ?? null,
      salesmanCode: readString(payload.salesmanCode) ?? null,
      salesmanName: readString(payload.salesman) ?? readString(payload.salesmanName) ?? null,
      qty: totals.qty,
      costPrice: totals.cost,
      sellingPrice: totals.selling,
      discount: totals.discount,
      marginAmount,
      marginPercentage,
      hasReturn: !!ret,
      returnNo: ret?.returnNo ?? null,
      hasAdjustment: !!adj,
      adjustmentNo: adj?.adjustmentNo ?? null,
    };
  }

  private matchesPosCounter(
    doc: { posCounter?: string; payload: Record<string, unknown> },
    posCounter?: string,
  ): boolean {
    if (!posCounter?.trim()) return true;
    const filter = posCounter.trim();
    const fromDoc = doc.posCounter ?? readString(doc.payload?.posCounter) ?? '';
    return fromDoc.toLowerCase() === filter.toLowerCase();
  }

  private matchesSalesman(
    doc: { payload: Record<string, unknown> },
    salesmanId?: string,
  ): boolean {
    if (!salesmanId?.trim()) return true;
    const id = readString(doc.payload?.salesmanId) ?? '';
    return id === salesmanId.trim();
  }

  private async loadReturnsByBillNos(
    storeId: string,
    billNos: string[],
  ): Promise<Map<string, { returnNo: string; payload: Record<string, unknown> }>> {
    const map = new Map<string, { returnNo: string; payload: Record<string, unknown> }>();
    if (billNos.length === 0) return map;

    const docs = await this.returnModel
      .find({
        storeId,
        $or: [
          { 'payload.originalBillNo': { $in: billNos } },
          { 'payload.billNo': { $in: billNos } },
        ],
      })
      .lean();

    for (const doc of docs) {
      const payload = doc.payload ?? {};
      const original =
        readString(payload.originalBillNo) ?? readString(payload.billNo) ?? '';
      if (!original) continue;
      map.set(original, { returnNo: doc.returnNo, payload });
    }
    return map;
  }

  private async loadAdjustmentsByBillNos(
    storeId: string,
    billNos: string[],
  ): Promise<Map<string, { adjustmentNo: string; payload: Record<string, unknown> }>> {
    const map = new Map<string, { adjustmentNo: string; payload: Record<string, unknown> }>();
    if (billNos.length === 0) return map;

    const docs = await this.adjustmentModel
      .find({
        storeId,
        'payload.originalBillNo': { $in: billNos },
      })
      .lean();

    for (const doc of docs) {
      const payload = doc.payload ?? {};
      const original = readString(payload.originalBillNo) ?? '';
      if (!original) continue;
      map.set(original, { adjustmentNo: doc.adjustmentNo, payload });
    }
    return map;
  }

  private async loadProductCostBySku(
    invoices: Array<{ payload: Record<string, unknown> }>,
  ): Promise<Map<string, number>> {
    const skus = new Set<string>();
    for (const inv of invoices) {
      const lines = inv.payload?.lines;
      if (!Array.isArray(lines)) continue;
      for (const line of lines) {
        if (!line || typeof line !== 'object') continue;
        const row = line as Record<string, unknown>;
        if (readNumberSafe(row.costPrice) > 0) continue;
        const sku = readString(row.sku) ?? readString(row.productCode);
        if (sku) skus.add(sku);
      }
    }
    if (skus.size === 0) return new Map();

    const products = await this.productModel
      .find({ sku: { $in: [...skus] } })
      .select('sku costPrice')
      .lean();
    return new Map(
      products.map((p) => [
        p.sku,
        typeof p.costPrice === 'number' && Number.isFinite(p.costPrice) ? p.costPrice : 0,
      ]),
    );
  }

  private async resolveStore(storeId?: string) {
    const code = storeId?.trim().toLowerCase();
    const store = code
      ? await this.storeModel.findOne({ code, status: 'active' }).lean()
      : await this.storeModel.findOne({ status: 'active' }).sort({ code: 1 }).lean();

    if (!store) {
      if (code) throw new NotFoundException(`Store '${code}' not found or inactive`);
      throw new NotFoundException('No active stores configured');
    }

    return { code: store.code, name: store.name };
  }
}

function readNumberSafe(value: unknown): number {
  if (value === undefined || value === null || value === '') return 0;
  const n = typeof value === 'number' ? value : Number(String(value).replace(/,/g, ''));
  return Number.isFinite(n) ? n : 0;
}

function docCreatedAt(doc: unknown): Date | undefined {
  if (!doc || typeof doc !== 'object') return undefined;
  const createdAt = (doc as { createdAt?: unknown }).createdAt;
  return createdAt instanceof Date ? createdAt : undefined;
}
