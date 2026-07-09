import { BadRequestException, Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import type { Model } from 'mongoose';
import { FilterQuery } from 'mongoose';
import { resolveDashboardStore } from '../dashboard/dashboard-store.util';
import {
  aggregateMarginLines,
  buildStoreSalePayloadTimeFilter,
  parseInvoiceDiscounts,
  parseInvoiceMarginLines,
  parseInvoiceNet,
  parsePaymentTotals,
  resolveBillsListDateRange,
  sumInvoiceLineQty,
  readNumber,
  readString,
} from '../dashboard/store-sales-payload.util';
import { Store, StoreDocument } from '../stores/schemas/store.schema';
import { Product, ProductDocument } from '../products/schemas/product.schema';
import { StoreInvoice, StoreInvoiceDocument } from '../store-sales/schemas/store-invoice.schema';
import {
  aggregateByGstRate,
  formatDocumentDateYmd,
  parseInvoiceGstLines,
  type GstDocumentContext,
} from './gst-report.util';
import type { BillSummaryReportResponse, BillSummaryRow, BillSummaryGstBucket } from './bill-summary.types';
import { BillSummaryQueryDto } from './dto/bill-summary-query.dto';

function formatCounter(posCounter?: string, deviceId?: string): string {
  const pos = posCounter?.trim() ?? '';
  const dev = deviceId?.trim() ?? '';
  if (pos && dev) return `POS${pos} · ${dev}`;
  if (pos) return `POS${pos}`;
  return dev;
}

function toGstBucket(row: {
  gstPercent: number;
  taxableAmount: number;
  taxAmount: number;
  sgstPercent: number;
  cgstPercent: number;
  igstPercent: number;
  sgstAmount: number;
  cgstAmount: number;
  igstAmount: number;
  totalInclusive: number;
}): BillSummaryGstBucket {
  return { ...row };
}

@Injectable()
export class BillSummaryService {
  constructor(
    @InjectModel(Store.name) private readonly storeModel: Model<StoreDocument>,
    @InjectModel(Product.name) private readonly productModel: Model<ProductDocument>,
    @InjectModel(StoreInvoice.name) private readonly invoiceModel: Model<StoreInvoiceDocument>,
  ) {}

  async buildReport(query: BillSummaryQueryDto): Promise<BillSummaryReportResponse> {
    const store = await resolveDashboardStore(this.storeModel, query.storeCode);
    const range = this.resolveRange(query);

    const baseFilter = buildStoreSalePayloadTimeFilter(store.code, range);
    const filter: FilterQuery<StoreInvoiceDocument> = { ...baseFilter } as FilterQuery<StoreInvoiceDocument>;
    if (query.posCounter?.trim()) {
      filter.posCounter = query.posCounter.trim();
    }

    const total = await this.invoiceModel.countDocuments(filter).lean();
    const limit = query.limit ?? 10000;
    const truncated = total > limit;

    const docs = await this.invoiceModel
      .find(filter)
      .sort({ createdAt: -1 })
      .limit(limit)
      .lean();

    const marginByBillNo = new Map<string, ReturnType<typeof parseInvoiceMarginLines>>();
    const skus = new Set<string>();

    const rows: BillSummaryRow[] = [];
    const gstPercentsSet = new Set<number>();

    // Pre-scan to collect costs + GST buckets without repeated parsing later.
    for (const inv of docs) {
      const payload = (inv.payload ?? {}) as Record<string, unknown>;
      const billNo = readString(payload.billNo) ?? inv.invoiceNo;
      const createdAt = (inv as { createdAt?: Date }).createdAt;
      const billDate = readString(payload.billDate) ?? formatDocumentDateYmd(payload.createdAtUtc, createdAt);

      const counter = formatCounter(inv.posCounter, inv.deviceId);
      const customerName = readString(payload.customerName) ?? '';

      const totalQty = sumInvoiceLineQty(payload);
      const billAmount = parseInvoiceNet(payload);
      const taxDiscount = parseInvoiceDiscounts(payload);
      const schemeDiscount = readNumber(payload.schemeDiscount);
      const discountAmount = taxDiscount + schemeDiscount;

      const cust = readString(payload.customerName);
      const docContext: GstDocumentContext = {
        documentKey: `${store.code}|invoice|${billNo}`,
        documentType: 'invoice',
        documentNo: billNo,
        documentDate: billDate,
        storeId: store.code,
        ...(cust ? { customerName: cust } : {}),
        isInterState: payload.isInterState === true || payload.isInterState === 'true',
      };

      const gstLines = parseInvoiceGstLines(payload, docContext);
      const byGstRate = aggregateByGstRate(gstLines);
      const bucketMap: Record<number, BillSummaryGstBucket> = {};
      for (const r of byGstRate) {
        bucketMap[r.gstPercent] = toGstBucket(r);
        gstPercentsSet.add(r.gstPercent);
      }

      // Totals derived from GST buckets (preferred) with fallback to payload totals.
      const gstTaxTotal = Object.values(bucketMap).reduce((s, b) => s + b.taxAmount, 0);
      const gstTaxableTotal = Object.values(bucketMap).reduce((s, b) => s + b.taxableAmount, 0);

      const cgstTotal = readNumber(payload.cgstTotal);
      const sgstTotal = readNumber(payload.sgstTotal);
      const igstTotal = readNumber(payload.igstTotal);
      const payloadTaxFallback = cgstTotal + sgstTotal + igstTotal;
      const taxAmount = gstTaxTotal > 0 ? gstTaxTotal : payloadTaxFallback;

      const goodsValue = gstTaxableTotal > 0 ? gstTaxableTotal : Math.max(0, billAmount - taxAmount);

      // Margin: collect per-invoice margin lines now, compute cost later.
      const marginLines = parseInvoiceMarginLines(payload);
      for (const l of marginLines) {
        if (l.sku && l.sku !== 'UNKNOWN') skus.add(l.sku);
      }
      marginByBillNo.set(billNo, marginLines);

      const payments = parsePaymentTotals(payload);

      const grossMargin = 0; // computed after loading costs

      rows.push({
        billDate,
        counter,
        purchaseBillNo: readString(payload.purchaseBillNo) ?? inv.invoiceNo,
        customerName,
        totalQty,
        goodsValue,
        discountAmount,
        taxAmount,
        billAmount,
        grossMargin,
        cashAmount: payments.cash,
        cardAmount: payments.card,
        creditNoteAmount: payments.creditNote,
        upiAmount: payments.upi,
        billNo,
        rrn: readString(payload.RRN) ?? readString(payload.rrn) ?? '',
        gstBuckets: bucketMap,
      });
    }

    const gstPercents = [...gstPercentsSet].sort((a, b) => a - b);

    // Load product cost by SKU for gross margin computations.
    let costBySku = new Map<string, number>();
    if (skus.size > 0) {
      const products = await this.productModel
        .find({ sku: { $in: [...skus] } })
        .select('sku costPrice')
        .lean();
      costBySku = new Map(products.map((p) => [p.sku, p.costPrice ?? 0]));
    }

    // Second pass: compute gross margin from parsed margin lines.
    const rowByBillNo = new Map<string, BillSummaryRow>();
    for (const r of rows) rowByBillNo.set(r.billNo, r);
    for (const [billNo, marginLines] of marginByBillNo.entries()) {
      const totals = aggregateMarginLines(marginLines, costBySku);
      const row = rowByBillNo.get(billNo);
      if (!row) continue;
      row.grossMargin = totals.salesMargin;
    }

    return {
      period: {
        from: query.from,
        to: query.to,
        storeCode: store.code,
        storeName: store.name,
      },
      truncated,
      total,
      gstPercents,
      data: rows,
    };
  }

  private resolveRange(query: BillSummaryQueryDto) {
    try {
      return resolveBillsListDateRange(query.from, query.to);
    } catch (err: unknown) {
      throw new BadRequestException(err instanceof Error ? err.message : String(err));
    }
  }
}

