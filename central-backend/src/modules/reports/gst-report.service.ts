import { BadRequestException, Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model } from 'mongoose';
import { isValidObjectIdString, toObjectId } from '../../common/object-id.util';
import { roundMoney } from '../../common/money.util';
import { resolveBillsListDateRange } from '../dashboard/store-sales-payload.util';
import { GoodsReceipt, GoodsReceiptDocument } from '../goods-receipts/schemas/goods-receipt.schema';
import { HsnCode, HsnCodeDocument } from '../hsn-codes/schemas/hsn-code.schema';
import {
  PurchaseOrder,
  PurchaseOrderDocument,
  PurchaseOrderLine,
} from '../purchase-orders/schemas/purchase-order.schema';
import { Product, ProductDocument } from '../products/schemas/product.schema';
import { StoreInvoice, StoreInvoiceDocument } from '../store-sales/schemas/store-invoice.schema';
import { StoreSaleReturn, StoreSaleReturnDocument } from '../store-sales/schemas/store-sale-return.schema';
import { GstReportQueryDto } from './dto/gst-report-query.dto';
import {
  buildSalePayloadTimeFilter,
  GstLineContribution,
  parseExchangeGstLines,
  parseInvoiceGstLines,
  parseReturnGstLines,
  summarizeGstRows,
} from './gst-report.util';
import { GstReportResult } from './gst-report.types';

function num(value: unknown, fallback = 0): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback;
}

@Injectable()
export class GstReportService {
  constructor(
    @InjectModel(StoreInvoice.name) private readonly invoiceModel: Model<StoreInvoiceDocument>,
    @InjectModel(StoreSaleReturn.name) private readonly returnModel: Model<StoreSaleReturnDocument>,
    @InjectModel(GoodsReceipt.name) private readonly grModel: Model<GoodsReceiptDocument>,
    @InjectModel(PurchaseOrder.name) private readonly poModel: Model<PurchaseOrderDocument>,
    @InjectModel(Product.name) private readonly productModel: Model<ProductDocument>,
    @InjectModel(HsnCode.name) private readonly hsnModel: Model<HsnCodeDocument>,
  ) {}

  async buildReport(query: GstReportQueryDto): Promise<GstReportResult> {
    let range;
    try {
      range = resolveBillsListDateRange(query.from, query.to);
    } catch {
      throw new BadRequestException('Invalid from or to date');
    }

    const salesRows = await this.buildSalesContributions(range, query.storeId);
    const purchaseRows = await this.buildPurchaseContributions(range);

    const sales = summarizeGstRows(salesRows.contributions, salesRows.documentCount);
    const purchase = summarizeGstRows(purchaseRows.contributions, purchaseRows.documentCount);

    const result: GstReportResult = {
      period: {
        from: range.fromYmd,
        to: range.toYmd,
      },
      sales: {
        summary: sales.summary,
        byGstRate: sales.byGstRate,
        byHsn: sales.byHsn,
      },
      purchase: {
        summary: purchase.summary,
        byGstRate: purchase.byGstRate,
        byHsn: purchase.byHsn,
      },
    };

    if (query.storeId?.trim()) result.period.storeId = query.storeId.trim();
    return result;
  }

  private async buildSalesContributions(
    range: ReturnType<typeof resolveBillsListDateRange>,
    storeId?: string,
  ): Promise<{ contributions: GstLineContribution[]; documentCount: number }> {
    const filter = buildSalePayloadTimeFilter(range, storeId);
    const [invoices, returns] = await Promise.all([
      this.invoiceModel.find(filter).lean(),
      this.returnModel.find(filter).lean(),
    ]);

    const contributions: GstLineContribution[] = [];
    for (const doc of invoices) {
      const payload = (doc.payload ?? {}) as Record<string, unknown>;
      contributions.push(...parseInvoiceGstLines(payload));
    }

    for (const doc of returns) {
      const payload = (doc.payload ?? {}) as Record<string, unknown>;
      contributions.push(...parseReturnGstLines(payload));
      if (doc.kind === 'exchange') {
        contributions.push(...parseExchangeGstLines(payload));
      }
    }

    return { contributions, documentCount: invoices.length + returns.length };
  }

  private async buildPurchaseContributions(
    range: ReturnType<typeof resolveBillsListDateRange>,
  ): Promise<{ contributions: GstLineContribution[]; documentCount: number }> {
    const filter: FilterQuery<GoodsReceiptDocument> = {
      status: 'posted',
      createdAt: { $gte: range.from, $lte: range.to },
    };

    const receipts = await this.grModel.find(filter).lean();
    if (receipts.length === 0) {
      return { contributions: [], documentCount: 0 };
    }

    const poIds = [
      ...new Set(
        receipts
          .map((gr) => gr.poId?.trim())
          .filter((id): id is string => !!id && isValidObjectIdString(id)),
      ),
    ];
    const poNos = [
      ...new Set(
        receipts
          .map((gr) => gr.poNo?.trim())
          .filter((no): no is string => !!no),
      ),
    ];

    const poById = new Map<string, PurchaseOrderDocument>();
    const poByPoNo = new Map<string, PurchaseOrderDocument>();
    if (poIds.length > 0 || poNos.length > 0) {
      const orConditions: FilterQuery<PurchaseOrderDocument>[] = [];
      if (poIds.length > 0) orConditions.push({ _id: { $in: poIds.map((id) => toObjectId(id)) } });
      if (poNos.length > 0) orConditions.push({ poNo: { $in: poNos } });
      const pos = await this.poModel.find({ $or: orConditions }).lean();
      for (const po of pos) {
        poById.set(String(po._id), po as PurchaseOrderDocument);
        if (typeof po.poNo === 'string') poByPoNo.set(po.poNo, po as PurchaseOrderDocument);
      }
    }

    const skus = new Set<string>();
    for (const gr of receipts) {
      for (const line of gr.lines ?? []) {
        const sku = line.sku?.trim();
        if (sku) skus.add(sku);
      }
    }

    const productBySku = new Map<string, ProductDocument>();
    const hsnById = new Map<string, string>();
    if (skus.size > 0) {
      const products = await this.productModel
        .find({ sku: { $in: [...skus] } })
        .select('sku hsnCodeId gstPercent')
        .lean();
      const hsnIds = new Set<string>();
      for (const product of products) {
        if (typeof product.sku === 'string') productBySku.set(product.sku, product as ProductDocument);
        if (product.hsnCodeId) hsnIds.add(String(product.hsnCodeId));
      }
      if (hsnIds.size > 0) {
        const hsnDocs = await this.hsnModel
          .find({ _id: { $in: [...hsnIds] } })
          .select('hsnCode gstPercent')
          .lean();
        for (const hsn of hsnDocs) {
          hsnById.set(String(hsn._id), hsn.hsnCode);
        }
      }
    }

    const contributions: GstLineContribution[] = [];
    for (const gr of receipts) {
      const po = this.resolvePurchaseOrder(gr, poById, poByPoNo);
      for (const line of gr.lines ?? []) {
        if ((line.outcome ?? 'valid') !== 'valid') continue;
        const qty = num(line.receivedQty, 0);
        if (qty <= 0) continue;
        const sku = line.sku?.trim();
        if (!sku) continue;

        const product = productBySku.get(sku);
        const poLine = this.findPoLine(po?.lines, line);
        const parsed = this.purchaseLineGst(sku, qty, poLine, product, hsnById);
        if (parsed) contributions.push(parsed);
      }
    }

    return { contributions, documentCount: receipts.length };
  }

  private resolvePurchaseOrder(
    gr: { poId?: string; poNo?: string },
    poById: Map<string, PurchaseOrderDocument>,
    poByPoNo: Map<string, PurchaseOrderDocument>,
  ): PurchaseOrderDocument | undefined {
    const poId = gr.poId?.trim();
    if (poId && poById.has(poId)) return poById.get(poId);
    const poNo = gr.poNo?.trim();
    if (poNo && poByPoNo.has(poNo)) return poByPoNo.get(poNo);
    return undefined;
  }

  private findPoLine(
    lines: PurchaseOrderLine[] | undefined,
    grLine: { sku?: string; productId?: unknown },
  ): PurchaseOrderLine | undefined {
    if (!lines?.length) return undefined;
    const sku = grLine.sku?.trim();
    if (sku) {
      const bySku = lines.find((l) => l.sku?.trim() === sku);
      if (bySku) return bySku;
    }
    const productId = grLine.productId ? String(grLine.productId) : '';
    if (productId) {
      return lines.find((l) => l.productId && String(l.productId) === productId);
    }
    return undefined;
  }

  private purchaseLineGst(
    sku: string,
    receivedQty: number,
    poLine: PurchaseOrderLine | undefined,
    product: ProductDocument | undefined,
    hsnById: Map<string, string>,
  ): GstLineContribution | null {
    const hsn = this.resolveHsn(product, hsnById);
    let gstPercent = num(poLine?.taxPercent, 0);
    if (gstPercent <= 0) gstPercent = num(product?.gstPercent, 0);

    let taxable = 0;
    let taxAmount = 0;

    if (poLine) {
      const poQty = Math.max(1, num(poLine.recdQty, 1));
      const factor = receivedQty / poQty;
      taxable = roundMoney(num(poLine.amount, 0) * factor);
      taxAmount = roundMoney(num(poLine.taxAmount, 0) * factor);
      if (taxAmount <= 0) {
        taxAmount = roundMoney(
          (num(poLine.cgstAmount, 0) + num(poLine.sgstAmount, 0)) * factor,
        );
      }
      if (gstPercent <= 0) gstPercent = num(poLine.taxPercent, 0);
    } else if (product) {
      const unitCost = num(product.costPrice, 0);
      taxable = roundMoney(unitCost * receivedQty);
      taxAmount = roundMoney(taxable * (gstPercent / 100));
    }

    if (taxable <= 0 && taxAmount <= 0) return null;

    const totalInclusive = roundMoney(taxable + taxAmount);
    return {
      hsn,
      gstPercent,
      qty: receivedQty,
      taxableAmount: taxable,
      taxAmount,
      totalInclusive,
    };
  }

  private resolveHsn(product: ProductDocument | undefined, hsnById: Map<string, string>): string {
    if (product?.hsnCodeId) {
      const fromMaster = hsnById.get(String(product.hsnCodeId));
      if (fromMaster) return fromMaster;
    }
    return 'UNKNOWN';
  }
}
