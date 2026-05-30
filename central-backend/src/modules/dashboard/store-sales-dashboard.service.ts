import { BadRequestException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { StoreCreditNote, StoreCreditNoteDocument } from '../store-sales/schemas/store-credit-note.schema';
import { StoreInvoice, StoreInvoiceDocument } from '../store-sales/schemas/store-invoice.schema';
import { StoreSaleReturn, StoreSaleReturnDocument } from '../store-sales/schemas/store-sale-return.schema';
import { Store, StoreDocument } from '../stores/schemas/store.schema';
import {
  bucketKeyForDate,
  isInRange,
  labelForBucketKey,
  parseInvoiceCreditApplied,
  parseInvoiceDiscounts,
  parseInvoiceGross,
  parseInvoiceLines,
  parseInvoiceNet,
  parseInvoicePayments,
  parseOccurredAt,
  parseReturnLineCount,
  parseReturnLineQty,
  readNumber,
  readString,
  resolveDateRange,
} from './store-sales-payload.util';
import type {
  StoreSalesCreditNoteDetailRow,
  StoreSalesDashboardOptions,
  StoreSalesDashboardResponse,
  StoreSalesDetailRow,
  StoreSalesPaymentMixRow,
  StoreSalesReturnDetailRow,
  StoreSalesTopProductRow,
} from './store-sales-dashboard.types';

type BucketAgg = {
  invoices: number;
  items: number;
  gross: number;
  net: number;
  returnsCount: number;
  returnValue: number;
};

@Injectable()
export class StoreSalesDashboardService {
  constructor(
    @InjectModel(StoreInvoice.name) private readonly invoiceModel: Model<StoreInvoiceDocument>,
    @InjectModel(StoreSaleReturn.name) private readonly returnModel: Model<StoreSaleReturnDocument>,
    @InjectModel(StoreCreditNote.name) private readonly creditNoteModel: Model<StoreCreditNoteDocument>,
    @InjectModel(Store.name) private readonly storeModel: Model<StoreDocument>,
  ) {}

  async getStoreSalesDashboard(options: StoreSalesDashboardOptions): Promise<StoreSalesDashboardResponse> {
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

    const [invoices, returns, creditNotes] = await Promise.all([
      this.invoiceModel.find({ storeId: store.code }).lean(),
      this.returnModel.find({ storeId: store.code }).lean(),
      this.creditNoteModel.find({ storeId: store.code }).lean(),
    ]);

    const buckets = new Map<string, BucketAgg>();
    const paymentTotals = new Map<string, { pieces: number; amount: number }>();
    const productTotals = new Map<string, { description: string; units: number }>();

    let grossSales = 0;
    let invoiceNetSum = 0;
    let invoiceCount = 0;
    let itemsSold = 0;
    let discountsTotal = 0;
    let creditAppliedOnBills = 0;

    for (const inv of invoices) {
      const payload = (inv.payload ?? {}) as Record<string, unknown>;
      const occurred = parseOccurredAt(payload, this.docTimestamp(inv));
      if (!occurred || !isInRange(occurred, range)) continue;

      const gross = parseInvoiceGross(payload);
      const net = parseInvoiceNet(payload);
      const items = parseInvoiceLines(payload).reduce((s, l) => s + l.qty, 0);
      const discounts = parseInvoiceDiscounts(payload);
      const creditApplied = parseInvoiceCreditApplied(payload);

      grossSales += gross;
      invoiceNetSum += net;
      invoiceCount += 1;
      itemsSold += items;
      discountsTotal += discounts;
      creditAppliedOnBills += creditApplied;

      const key = bucketKeyForDate(occurred, range.bucketByMonth);
      this.ensureBucket(buckets, key);
      const b = buckets.get(key)!;
      b.invoices += 1;
      b.items += items;
      b.gross += gross;
      b.net += net;

      for (const pay of parseInvoicePayments(payload)) {
        const cur = paymentTotals.get(pay.mode) ?? { pieces: 0, amount: 0 };
        paymentTotals.set(pay.mode, {
          pieces: cur.pieces + 1,
          amount: cur.amount + pay.amount,
        });
      }

      for (const line of parseInvoiceLines(payload)) {
        const cur = productTotals.get(line.sku) ?? { description: line.description, units: 0 };
        productTotals.set(line.sku, {
          description: line.description || cur.description,
          units: cur.units + line.qty,
        });
      }
    }

    let returnValue = 0;
    let returnsCount = 0;
    const returnDetails: StoreSalesReturnDetailRow[] = [];

    for (const ret of returns) {
      const payload = (ret.payload ?? {}) as Record<string, unknown>;
      const occurred = parseOccurredAt(payload, this.docTimestamp(ret));
      if (!occurred || !isInRange(occurred, range)) continue;

      const total = readNumber(payload.returnTotal);
      const returnQty = parseReturnLineQty(payload);
      returnValue += total;
      returnsCount += 1;
      itemsSold -= returnQty;

      const key = bucketKeyForDate(occurred, range.bucketByMonth);
      this.ensureBucket(buckets, key);
      const b = buckets.get(key)!;
      b.returnsCount += 1;
      b.returnValue += total;

      returnDetails.push({
        returnNo: ret.returnNo,
        kind: ret.kind,
        originalBillNo: readString(payload.originalBillNo) ?? null,
        returnMode: readString(payload.returnMode) ?? null,
        reason: readString(payload.reason) ?? null,
        returnTotal: total,
        replacementTotal: readNumber(payload.replacementTotal),
        creditBalance: readNumber(payload.creditBalance),
        lineCount: parseReturnLineCount(payload),
        creditNoteNo: readString(payload.creditNoteNo) ?? null,
        customerName: readString(payload.customerName) ?? null,
        customerPhone: readString(payload.customerPhone) ?? null,
        occurredAt: occurred.toISOString(),
      });
    }

    returnDetails.sort(
      (a, b) => new Date(b.occurredAt).getTime() - new Date(a.occurredAt).getTime(),
    );

    const netSales = invoiceNetSum - returnValue;

    const periodCreditNotes = creditNotes.filter((cn) => {
      const created = this.parseDocDate(this.docTimestamp(cn));
      return created !== null && isInRange(created, range);
    });

    let creditNotesIssuedAmount = 0;
    let creditRemainingOutstanding = 0;
    let availableCount = 0;
    let consumedCount = 0;
    let appliedFromNotes = 0;

    const creditNoteItems: StoreSalesCreditNoteDetailRow[] = periodCreditNotes
      .map((cn) => {
        creditNotesIssuedAmount += cn.amount ?? 0;
        creditRemainingOutstanding += cn.remainingAmount ?? 0;
        appliedFromNotes += cn.totalApplied ?? 0;
        if (cn.status === 'available') availableCount += 1;
        if (cn.status === 'consumed') consumedCount += 1;

        const created = this.toIso(this.docTimestamp(cn));

        return {
          creditNoteNo: cn.creditNoteNo,
          status: cn.status,
          amount: cn.amount,
          remainingAmount: cn.remainingAmount,
          totalApplied: cn.totalApplied ?? 0,
          returnNo: cn.returnNo ?? null,
          originalBillNo: cn.originalBillNo ?? null,
          customerCode: cn.customerCode ?? null,
          customerPhone: cn.customerPhone ?? null,
          customerName: cn.customerName ?? null,
          lastAppliedBillNo: cn.lastAppliedBillNo ?? null,
          consumedBillNo: cn.consumedBillNo ?? null,
          createdAt: created,
          applications: (cn.applications ?? []).map((a) => ({
            billNo: a.billNo,
            amountApplied: a.amountApplied,
            appliedAt: a.appliedAt ?? null,
          })),
        };
      })
      .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
      .slice(0, options.creditNoteLimit);

    const salesDetails = this.buildSalesDetails(buckets, range.bucketByMonth);
    const paymentMix = this.buildPaymentMix(paymentTotals);
    const topProducts = this.buildTopProducts(productTotals, options.topProductLimit);

    return {
      store,
      period: {
        preset: options.period,
        from: range.fromYmd,
        to: range.toYmd,
        label: range.label,
      },
      summary: {
        grossSales,
        netSales,
        invoices: invoiceCount,
        avgBasket: invoiceCount > 0 ? Math.round(netSales / invoiceCount) : 0,
        itemsSold: Math.max(0, itemsSold),
        returnsCount,
        returnValue,
        discountsTotal,
        creditNotesIssued: periodCreditNotes.length,
        creditNotesIssuedAmount,
        creditAppliedOnBills,
        creditRemainingOutstanding,
      },
      salesDetails,
      paymentMix,
      topProducts,
      returns: returnDetails.slice(0, options.returnDetailLimit),
      creditNotes: {
        summary: {
          issuedCount: periodCreditNotes.length,
          issuedAmount: creditNotesIssuedAmount,
          appliedAmount: appliedFromNotes,
          remainingAmount: creditRemainingOutstanding,
          availableCount,
          consumedCount,
        },
        items: creditNoteItems,
      },
    };
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

  private ensureBucket(map: Map<string, BucketAgg>, key: string) {
    if (!map.has(key)) {
      map.set(key, {
        invoices: 0,
        items: 0,
        gross: 0,
        net: 0,
        returnsCount: 0,
        returnValue: 0,
      });
    }
  }

  private buildSalesDetails(
    buckets: Map<string, BucketAgg>,
    bucketByMonth: boolean,
  ): StoreSalesDetailRow[] {
    return [...buckets.entries()]
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([bucketKey, agg]) => ({
        bucketKey,
        label: labelForBucketKey(bucketKey, bucketByMonth),
        invoices: agg.invoices,
        items: agg.items,
        gross: agg.gross,
        net: agg.net - agg.returnValue,
        returnsCount: agg.returnsCount,
        returnValue: agg.returnValue,
      }));
  }

  private buildPaymentMix(
    paymentTotals: Map<string, { pieces: number; amount: number }>,
  ): StoreSalesPaymentMixRow[] {
    const totalPieces = [...paymentTotals.values()].reduce((s, v) => s + v.pieces, 0);
    return [...paymentTotals.entries()]
      .map(([mode, v]) => ({
        mode,
        pieces: v.pieces,
        amount: v.amount,
        percent: totalPieces > 0 ? Math.round((v.pieces / totalPieces) * 100) : 0,
      }))
      .sort((a, b) => b.pieces - a.pieces);
  }

  private docTimestamp(doc: Record<string, unknown>): unknown {
    return doc.updatedAt ?? doc.createdAt;
  }

  private parseDocDate(value: unknown): Date | null {
    if (value instanceof Date && !Number.isNaN(value.getTime())) return value;
    if (typeof value === 'string' || typeof value === 'number') {
      const d = new Date(value);
      if (!Number.isNaN(d.getTime())) return d;
    }
    return null;
  }

  private toIso(value: unknown): string {
    const d = this.parseDocDate(value);
    return d ? d.toISOString() : new Date().toISOString();
  }

  private buildTopProducts(
    productTotals: Map<string, { description: string; units: number }>,
    limit: number,
  ): StoreSalesTopProductRow[] {
    const totalUnits = [...productTotals.values()].reduce((s, v) => s + v.units, 0);
    return [...productTotals.entries()]
      .map(([sku, v]) => ({
        sku,
        description: v.description,
        units: v.units,
        percent: totalUnits > 0 ? Math.round((v.units / totalUnits) * 100) : 0,
      }))
      .sort((a, b) => b.units - a.units)
      .slice(0, limit);
  }
}
