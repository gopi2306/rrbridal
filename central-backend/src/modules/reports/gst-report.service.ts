import { BadRequestException, Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model } from 'mongoose';
import { isValidObjectIdString, toObjectId } from '../../common/object-id.util';
import { roundMoney } from '../../common/money.util';
import { readString } from '../dashboard/store-sales-payload.util';
import { resolveBillsListDateRange } from '../dashboard/store-sales-payload.util';
import { GoodsReceipt, GoodsReceiptDocument } from '../goods-receipts/schemas/goods-receipt.schema';
import { HsnCode, HsnCodeDocument } from '../hsn-codes/schemas/hsn-code.schema';
import {
  PurchaseOrder,
  PurchaseOrderDocument,
  PurchaseOrderLine,
} from '../purchase-orders/schemas/purchase-order.schema';
import { Product, ProductDocument } from '../products/schemas/product.schema';
import { Supplier, SupplierDocument } from '../suppliers/schemas/supplier.schema';
import { StoreInvoice, StoreInvoiceDocument } from '../store-sales/schemas/store-invoice.schema';
import { StoreSaleReturn, StoreSaleReturnDocument } from '../store-sales/schemas/store-sale-return.schema';
import { GstReportQueryDto } from './dto/gst-report-query.dto';
import {
  buildPurchaseTaxComponents,
  buildSalePayloadTimeFilter,
  formatDocumentDateYmd,
  GstDocumentContext,
  GstLineContribution,
  parseExchangeGstLines,
  parseInvoiceGstLines,
  parseReturnGstLines,
  summarizeGstRows,
} from './gst-report.util';
import {
  GstPurchaseReportResult,
  GstPurchaseReportSection,
  GstReportResult,
  GstSalesReportResult,
  GstSalesReportSection,
} from './gst-report.types';

function num(value: unknown, fallback = 0): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback;
}

function readDocCreatedAt(doc: unknown): Date | undefined {
  if (doc && typeof doc === 'object' && 'createdAt' in doc) {
    const createdAt = (doc as { createdAt?: unknown }).createdAt;
    if (createdAt instanceof Date) return createdAt;
  }
  return undefined;
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
    @InjectModel(Supplier.name) private readonly supplierModel: Model<SupplierDocument>,
  ) {}

  async buildReport(query: GstReportQueryDto): Promise<GstReportResult> {
    const range = this.resolveRange(query);
    const sales = await this.buildSalesSection(range, query.storeId);
    const purchase = await this.buildPurchaseSection(range);

    const result: GstReportResult = {
      period: { from: range.fromYmd, to: range.toYmd },
      sales,
      purchase,
    };
    if (query.storeId?.trim()) result.period.storeId = query.storeId.trim();
    return result;
  }

  async buildSalesReport(query: GstReportQueryDto): Promise<GstSalesReportResult> {
    const range = this.resolveRange(query);
    const section = await this.buildSalesSection(range, query.storeId);
    const result: GstSalesReportResult = {
      period: { from: range.fromYmd, to: range.toYmd },
      ...section,
    };
    if (query.storeId?.trim()) result.period.storeId = query.storeId.trim();
    return result;
  }

  async buildPurchaseReport(query: GstReportQueryDto): Promise<GstPurchaseReportResult> {
    const range = this.resolveRange(query);
    const section = await this.buildPurchaseSection(range);
    return {
      period: { from: range.fromYmd, to: range.toYmd },
      ...section,
    };
  }

  private resolveRange(query: GstReportQueryDto) {
    try {
      return resolveBillsListDateRange(query.from, query.to);
    } catch {
      throw new BadRequestException('Invalid from or to date');
    }
  }

  private async buildSalesSection(
    range: ReturnType<typeof resolveBillsListDateRange>,
    storeId?: string,
  ): Promise<GstSalesReportSection> {
    const salesRows = await this.buildSalesContributions(range, storeId);
    const summarized = summarizeGstRows(salesRows.contributions, salesRows.documentCount, {
      section: 'sales',
    });
    return {
      summary: summarized.summary,
      byGstRate: summarized.byGstRate,
      byHsn: summarized.byHsn,
      byItem: summarized.byItem,
      byInvoice: summarized.byInvoice as GstSalesReportSection['byInvoice'],
    };
  }

  private async buildPurchaseSection(
    range: ReturnType<typeof resolveBillsListDateRange>,
  ): Promise<GstPurchaseReportSection> {
    const purchaseRows = await this.buildPurchaseContributions(range);
    const summarized = summarizeGstRows(purchaseRows.contributions, purchaseRows.documentCount, {
      section: 'purchase',
      productNames: purchaseRows.productNames,
    });
    return {
      summary: summarized.summary,
      byGstRate: summarized.byGstRate,
      byHsn: summarized.byHsn,
      byItem: summarized.byItem,
      byInvoice: summarized.byInvoice as GstPurchaseReportSection['byInvoice'],
    };
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
      const document = this.salesDocumentContext(
        doc.storeId,
        doc.invoiceNo,
        'invoice',
        payload,
        readDocCreatedAt(doc),
      );
      contributions.push(...parseInvoiceGstLines(payload, document));
    }

    for (const doc of returns) {
      const payload = (doc.payload ?? {}) as Record<string, unknown>;
      const documentType = doc.kind === 'exchange' ? 'exchange' : 'return';
      const document = this.salesDocumentContext(
        doc.storeId,
        doc.returnNo,
        documentType,
        payload,
        readDocCreatedAt(doc),
      );
      contributions.push(...parseReturnGstLines(payload, document));
      if (doc.kind === 'exchange') {
        contributions.push(...parseExchangeGstLines(payload, document));
      }
    }

    return { contributions, documentCount: invoices.length + returns.length };
  }

  private salesDocumentContext(
    storeId: string,
    documentNo: string,
    documentType: GstDocumentContext['documentType'],
    payload: Record<string, unknown>,
    createdAt?: Date,
  ): GstDocumentContext {
    const documentDate =
      formatDocumentDateYmd(payload.createdAtUtc, createdAt) ||
      formatDocumentDateYmd(createdAt);
    const customerName = readString(payload.customerName);
    const isInterState =
      payload.isInterState === true || payload.isInterState === 'true';
    const documentKey = `${storeId}|${documentType}|${documentNo}`;
    const context: GstDocumentContext = {
      documentKey,
      documentType,
      documentNo,
      documentDate,
      storeId,
    };
    if (customerName) context.customerName = customerName;
    if (isInterState) context.isInterState = true;
    return context;
  }

  private async buildPurchaseContributions(
    range: ReturnType<typeof resolveBillsListDateRange>,
  ): Promise<{
    contributions: GstLineContribution[];
    documentCount: number;
    productNames: Map<string, string>;
  }> {
    const filter: FilterQuery<GoodsReceiptDocument> = {
      status: 'posted',
      createdAt: { $gte: range.from, $lte: range.to },
    };

    const receipts = await this.grModel.find(filter).lean();
    if (receipts.length === 0) {
      return { contributions: [], documentCount: 0, productNames: new Map() };
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
    const supplierIds = new Set<string>();
    for (const gr of receipts) {
      if (gr.supplier?.supplierId) supplierIds.add(gr.supplier.supplierId);
      for (const line of gr.lines ?? []) {
        const sku = line.sku?.trim();
        if (sku) skus.add(sku);
      }
    }

    const productBySku = new Map<string, ProductDocument>();
    const productNames = new Map<string, string>();
    const hsnById = new Map<string, string>();
    if (skus.size > 0) {
      const products = await this.productModel
        .find({ sku: { $in: [...skus] } })
        .select('sku itemName hsnCodeId gstPercent costPrice')
        .lean();
      const hsnIds = new Set<string>();
      for (const product of products) {
        if (typeof product.sku === 'string') {
          productBySku.set(product.sku, product as ProductDocument);
          if (product.itemName) productNames.set(product.sku, product.itemName);
        }
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

    const supplierGstById = new Map<string, string>();
    if (supplierIds.size > 0) {
      const objectIds = [...supplierIds].filter((id) => isValidObjectIdString(id));
      if (objectIds.length > 0) {
        const suppliers = await this.supplierModel
          .find({ _id: { $in: objectIds.map((id) => toObjectId(id)) } })
          .select('gstNumber')
          .lean();
        for (const supplier of suppliers) {
          if (supplier.gstNumber) {
            supplierGstById.set(String(supplier._id), supplier.gstNumber);
          }
        }
      }
    }

    const contributions: GstLineContribution[] = [];
    for (const gr of receipts) {
      const po = this.resolvePurchaseOrder(gr, poById, poByPoNo);
      const supplierId = gr.supplier?.supplierId;
      const supplierGstNumber = supplierId ? supplierGstById.get(supplierId) : undefined;
      const document = this.purchaseDocumentContext(gr, supplierGstNumber);
      for (const line of gr.lines ?? []) {
        if ((line.outcome ?? 'valid') !== 'valid') continue;
        const qty = num(line.receivedQty, 0);
        if (qty <= 0) continue;
        const sku = line.sku?.trim();
        if (!sku) continue;

        const product = productBySku.get(sku);
        const poLine = this.findPoLine(po?.lines, line);
        const parsed = this.purchaseLineGst(
          sku,
          qty,
          poLine,
          product,
          hsnById,
          document,
          line.description,
        );
        if (parsed) contributions.push(parsed);
      }
    }

    return { contributions, documentCount: receipts.length, productNames };
  }

  private purchaseDocumentContext(
    gr: {
      receiptNo: string;
      grnNumber?: string;
      poNo?: string;
      invoiceNo?: string;
      invoiceDate?: string;
      supplier?: { name?: string };
      createdAt?: Date;
    },
    supplierGstNumber?: string,
  ): GstDocumentContext {
    const receiptNo = gr.receiptNo;
    const context: GstDocumentContext = {
      documentKey: receiptNo,
      documentType: 'purchase',
      documentNo: receiptNo,
      documentDate: formatDocumentDateYmd(gr.createdAt),
      receiptNo,
    };
    if (gr.grnNumber) context.grnNumber = gr.grnNumber;
    if (gr.poNo) context.poNo = gr.poNo;
    if (gr.invoiceNo) context.invoiceNo = gr.invoiceNo;
    if (gr.invoiceDate) context.invoiceDate = gr.invoiceDate;
    if (gr.supplier?.name) context.supplierName = gr.supplier.name;
    if (supplierGstNumber) context.supplierGstNumber = supplierGstNumber;
    return context;
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
    document: GstDocumentContext,
    lineDescription?: string,
  ): GstLineContribution | null {
    const hsn = this.resolveHsn(product, hsnById);
    let gstPercent = num(poLine?.taxPercent, 0);
    if (gstPercent <= 0) gstPercent = num(product?.gstPercent, 0);

    let taxable = 0;
    let taxAmount = 0;
    let factor = 1;

    if (poLine) {
      const poQty = Math.max(1, num(poLine.recdQty, 1));
      factor = receivedQty / poQty;
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

    const poTaxLine = poLine
      ? {
          ...(poLine.taxPercent !== undefined ? { taxPercent: poLine.taxPercent } : {}),
          ...(poLine.cgstPercent !== undefined ? { cgstPercent: poLine.cgstPercent } : {}),
          ...(poLine.sgstPercent !== undefined ? { sgstPercent: poLine.sgstPercent } : {}),
          cgstAmount: roundMoney(num(poLine.cgstAmount, 0) * factor),
          sgstAmount: roundMoney(num(poLine.sgstAmount, 0) * factor),
        }
      : undefined;

    const tax = buildPurchaseTaxComponents(taxable, taxAmount, poTaxLine, gstPercent);
    const totalInclusive = roundMoney(taxable + tax.taxAmount);
    const itemName = lineDescription?.trim() || product?.itemName;

    const line: GstLineContribution = {
      hsn,
      sku,
      qty: receivedQty,
      taxableAmount: taxable,
      totalInclusive,
      ...tax,
      documentKey: document.documentKey,
      documentType: document.documentType,
      documentNo: document.documentNo,
      documentDate: document.documentDate,
    };
    if (itemName) line.itemName = itemName;
    if (document.grnNumber) line.grnNumber = document.grnNumber;
    if (document.receiptNo) line.receiptNo = document.receiptNo;
    if (document.poNo) line.poNo = document.poNo;
    if (document.invoiceNo) line.invoiceNo = document.invoiceNo;
    if (document.invoiceDate) line.invoiceDate = document.invoiceDate;
    if (document.supplierName) line.supplierName = document.supplierName;
    if (document.supplierGstNumber) line.supplierGstNumber = document.supplierGstNumber;
    return line;
  }

  private resolveHsn(product: ProductDocument | undefined, hsnById: Map<string, string>): string {
    if (product?.hsnCodeId) {
      const fromMaster = hsnById.get(String(product.hsnCodeId));
      if (fromMaster) return fromMaster;
    }
    return 'UNKNOWN';
  }
}
