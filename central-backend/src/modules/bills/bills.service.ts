import { BadRequestException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { roundMoney } from '../../common/money.util';
import { resolveDashboardStore } from '../dashboard/dashboard-store.util';
import {
  buildStoreSalePayloadTimeFilter,
  formatBillStatusLabel,
  parseInvoiceCreditApplied,
  parseInvoiceNet,
  parseOccurredAt,
  parsePaymentTotals,
  readNumber,
  readString,
  resolveBillPaymentLabel,
  resolveBillStatusKey,
  resolveBillsListDateRange,
  sumInvoiceLineQty,
} from '../dashboard/store-sales-payload.util';
import { Store, StoreDocument } from '../stores/schemas/store.schema';
import { StoreAdjustment, StoreAdjustmentDocument } from '../store-sales/schemas/store-adjustment.schema';
import { StoreInvoice, StoreInvoiceDocument } from '../store-sales/schemas/store-invoice.schema';
import { StoreSaleReturn, StoreSaleReturnDocument } from '../store-sales/schemas/store-sale-return.schema';
import type {
  BillDetailLine,
  BillDetailPayment,
  BillDetailResponse,
  BillListRow,
  BillsListOptions,
  BillsListPage,
} from './bills.types';

type InvoiceLeanDoc = {
  invoiceNo: string;
  posCounter?: string;
  createdAt?: Date;
  payload?: Record<string, unknown>;
};

@Injectable()
export class BillsService {
  constructor(
    @InjectModel(StoreInvoice.name) private readonly invoiceModel: Model<StoreInvoiceDocument>,
    @InjectModel(StoreSaleReturn.name) private readonly returnModel: Model<StoreSaleReturnDocument>,
    @InjectModel(StoreAdjustment.name) private readonly adjustmentModel: Model<StoreAdjustmentDocument>,
    @InjectModel(Store.name) private readonly storeModel: Model<StoreDocument>,
  ) {}

  async listBills(options: BillsListOptions): Promise<BillsListPage> {
    const store = await resolveDashboardStore(this.storeModel, options.storeCode);
    let range;
    try {
      range = resolveBillsListDateRange(options.from, options.to);
    } catch {
      throw new BadRequestException('Invalid from or to date');
    }

    const baseFilter = buildStoreSalePayloadTimeFilter(store.code, range);
    const filters: Record<string, unknown>[] = [baseFilter];
    if (options.search?.trim()) filters.push(this.buildSearchFilter(options.search.trim()));
    if (options.salesmanCode?.trim()) filters.push(this.buildSalesmanFilter(options.salesmanCode.trim()));
    const filter = filters.length === 1 ? baseFilter : { $and: filters };

    const hasPostFilters = Boolean(options.status || options.paymentMode);
    const page = options.page;
    const limit = options.limit;

    const returnDocs = await this.returnModel.find({ storeId: store.code }).lean();
    const returnsByBill = this.groupReturnsByBill(returnDocs);

    if (!hasPostFilters) {
      const total = await this.invoiceModel.countDocuments(filter);
      const totalPages = total === 0 ? 0 : Math.ceil(total / limit);
      const docs = await this.invoiceModel
        .find(filter)
        .sort({ createdAt: -1 })
        .skip((page - 1) * limit)
        .limit(limit)
        .lean();

      const data = docs.map((doc) =>
        this.toListRow(doc, store, returnsByBill.get(doc.invoiceNo) ?? []),
      );

      return {
        storeCode: store.code,
        storeName: store.name,
        from: range.fromYmd,
        to: range.toYmd,
        data,
        total,
        page,
        limit,
        totalPages,
      };
    }

    const docs = await this.invoiceModel.find(filter).sort({ createdAt: -1 }).lean();
    let rows = docs.map((doc) =>
      this.toListRow(doc, store, returnsByBill.get(doc.invoiceNo) ?? []),
    );

    if (options.status) {
      rows = rows.filter((r) => r.statusKey === options.status);
    }
    if (options.paymentMode) {
      rows = rows.filter((r) => r.paymentModeKey === options.paymentMode);
    }

    const total = rows.length;
    const totalPages = total === 0 ? 0 : Math.ceil(total / limit);
    const data = rows.slice((page - 1) * limit, page * limit);

    return {
      storeCode: store.code,
      storeName: store.name,
      from: range.fromYmd,
      to: range.toYmd,
      data,
      total,
      page,
      limit,
      totalPages,
    };
  }

  async getBillDetail(storeCode: string | undefined, billNo: string): Promise<BillDetailResponse> {
    const store = await resolveDashboardStore(this.storeModel, storeCode);
    const trimmedBillNo = billNo.trim();
    if (!trimmedBillNo) {
      throw new BadRequestException('billNo is required');
    }

    const doc = await this.invoiceModel
      .findOne({ storeId: store.code, invoiceNo: trimmedBillNo })
      .lean() as InvoiceLeanDoc | null;
    if (!doc) {
      throw new NotFoundException(`Bill '${trimmedBillNo}' not found for store '${store.code}'`);
    }

    const payload = (doc.payload ?? {}) as Record<string, unknown>;
    const returnDocs = await this.returnModel
      .find({ storeId: store.code, 'payload.originalBillNo': trimmedBillNo })
      .lean();
    const returnPayloads = returnDocs.map((r) => (r.payload ?? {}) as Record<string, unknown>);
    const statusKey = resolveBillStatusKey(payload, returnPayloads);
    const payment = resolveBillPaymentLabel(parsePaymentTotals(payload));
    const occurred = parseOccurredAt(payload, doc.createdAt);

    const adjustmentDoc = await this.adjustmentModel
      .findOne({ storeId: store.code, 'payload.originalBillNo': trimmedBillNo })
      .lean();

    const returnDoc = returnDocs[0];
    const returnPayload = returnDoc ? ((returnDoc.payload ?? {}) as Record<string, unknown>) : null;

    return {
      billNo: readString(payload.billNo) ?? doc.invoiceNo,
      billDate: readString(payload.billDate) ?? null,
      occurredAt: occurred?.toISOString() ?? new Date(doc.createdAt ?? Date.now()).toISOString(),
      storeCode: store.code,
      storeName: store.name,
      posCounter: readString(payload.posCounter) ?? doc.posCounter ?? null,
      customerCode: readString(payload.customerCode) ?? null,
      customerName: readString(payload.customerName) ?? null,
      customerPhone: readString(payload.customerPhone) ?? null,
      salesman: readString(payload.salesman) ?? readString(payload.salesmanName) ?? null,
      salesmanCode: readString(payload.salesmanCode) ?? null,
      salesmanId: readString(payload.salesmanId) ?? null,
      holdBills: Boolean(payload.holdBills),
      doorDelivery: Boolean(payload.doorDelivery),
      onlineCod: Boolean(payload.onlineCodOrder ?? payload.onlineCod),
      stitching: Boolean(payload.stitching),
      isInterState: Boolean(payload.isInterState),
      deliveryDate: readString(payload.deliveryDate) ?? null,
      printInvoice: Boolean(payload.printInvoice),
      status: formatBillStatusLabel(statusKey),
      statusKey,
      paymentMode: payment.label,
      paymentModeKey: payment.key,
      totals: {
        subTotal: readNumber(payload.subTotal),
        itemDiscount: readNumber(payload.itemDiscount),
        cashDiscount: readNumber(payload.cashDiscAmount),
        schemeDiscount: readNumber(payload.schemeDiscount),
        roundOff: readNumber(payload.roundOff),
        payable: parseInvoiceNet(payload),
        originalTaxTotal: readNumber(payload.originalTaxTotal ?? payload.taxTotal),
        revisedSubTotal: readNumber(payload.revisedSubTotal),
        cgstTotal: readNumber(payload.cgstTotal),
        sgstTotal: readNumber(payload.sgstTotal),
        igstTotal: readNumber(payload.igstTotal),
        creditApplied: parseInvoiceCreditApplied(payload),
      },
      lines: this.mapDetailLines(payload),
      payments: this.mapDetailPayments(payload),
      linkedReturn: returnDoc
        ? {
            returnNo: returnDoc.returnNo,
            mode: readString(returnPayload?.returnMode) ?? null,
            creditNoteNo: readString(returnPayload?.creditNoteNo) ?? null,
          }
        : null,
      linkedAdjustment: adjustmentDoc
        ? { adjustmentNo: adjustmentDoc.adjustmentNo }
        : null,
    };
  }

  private toListRow(
    doc: InvoiceLeanDoc,
    store: { code: string; name: string },
    returnPayloads: Record<string, unknown>[],
  ): BillListRow {
    const payload = (doc.payload ?? {}) as Record<string, unknown>;
    const occurred = parseOccurredAt(payload, doc.createdAt);
    const statusKey = resolveBillStatusKey(payload, returnPayloads);
    const payment = resolveBillPaymentLabel(parsePaymentTotals(payload));

    return {
      billNo: readString(payload.billNo) ?? doc.invoiceNo,
      occurredAt: occurred?.toISOString() ?? new Date(doc.createdAt ?? Date.now()).toISOString(),
      storeCode: store.code,
      storeName: store.name,
      customerName: readString(payload.customerName) ?? null,
      salesmanCode: readString(payload.salesmanCode) ?? null,
      salesmanId: readString(payload.salesmanId) ?? null,
      salesmanName: readString(payload.salesman) ?? readString(payload.salesmanName) ?? null,
      itemCount: sumInvoiceLineQty(payload),
      netAmount: parseInvoiceNet(payload),
      paymentMode: payment.label,
      status: formatBillStatusLabel(statusKey),
      statusKey,
      paymentModeKey: payment.key,
    };
  }

  private groupReturnsByBill(
    returnDocs: ReadonlyArray<{ payload?: Record<string, unknown> }>,
  ): Map<string, Record<string, unknown>[]> {
    const map = new Map<string, Record<string, unknown>[]>();
    for (const doc of returnDocs) {
      const payload = (doc.payload ?? {}) as Record<string, unknown>;
      const billNo = readString(payload.originalBillNo);
      if (!billNo) continue;
      const list = map.get(billNo) ?? [];
      list.push(payload);
      map.set(billNo, list);
    }
    return map;
  }

  private buildSearchFilter(search: string): Record<string, unknown> {
    const escaped = search.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const re = new RegExp(escaped, 'i');
    return {
      $or: [
        { invoiceNo: re },
        { 'payload.billNo': re },
        { 'payload.customerName': re },
        { 'payload.salesman': re },
        { 'payload.salesmanCode': re },
        { 'payload.customerPhone': re },
        { 'payload.lines.sku': re },
        { 'payload.lines.productCode': re },
        { 'payload.lines.barcode': re },
        { 'payload.lines.description': re },
      ],
    };
  }

  private buildSalesmanFilter(salesmanCode: string): Record<string, unknown> {
    if (salesmanCode === '__legacy__') {
      return {
        $or: [
          { 'payload.salesmanCode': { $exists: false } },
          { 'payload.salesmanCode': null },
          { 'payload.salesmanCode': '' },
        ],
      };
    }

    const escaped = salesmanCode.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const re = new RegExp(`^${escaped}$`, 'i');
    return {
      $or: [
        { 'payload.salesmanCode': re },
        { 'payload.salesman': re },
      ],
    };
  }

  private mapDetailLines(payload: Record<string, unknown>): BillDetailLine[] {
    const lines = payload.lines;
    if (!Array.isArray(lines)) return [];

    const result: BillDetailLine[] = [];
    let lineNo = 0;
    for (const line of lines) {
      if (!line || typeof line !== 'object') continue;
      const row = line as Record<string, unknown>;
      const qty = readNumber(row.qty);
      if (qty <= 0) continue;
      lineNo += 1;
      result.push({
        lineNo,
        sku: readString(row.sku) ?? readString(row.productCode) ?? 'UNKNOWN',
        description: readString(row.description) ?? readString(row.sku) ?? 'Product',
        hsn: readString(row.hsn) ?? null,
        qty,
        rate: readNumber(row.rate),
        amount: readNumber(row.amount),
        mrp: readNumber(row.mrp),
        taxPercent: readNumber(row.taxPercent),
        taxAmount: readNumber(row.taxAmount),
        cgstPercent: readNumber(row.cgstPercent),
        sgstPercent: readNumber(row.sgstPercent),
        igstPercent: readNumber(row.igstPercent),
        alterationAmount: readNumber(row.alterationAmount),
        discountAmount: readNumber(row.discountAmount ?? row.itemDiscountAmount),
        cashDiscountAmount: readNumber(row.cashDiscountAmount),
        schemeDiscountAmount: readNumber(row.schemeDiscountAmount),
        revisedAmount: readNumber(row.revisedAmount),
        revisedTaxAmount: readNumber(row.revisedTaxAmount),
      });
    }
    return result;
  }

  private mapDetailPayments(payload: Record<string, unknown>): BillDetailPayment[] {
    const payments = payload.payments;
    if (!Array.isArray(payments)) {
      const totals = parsePaymentTotals(payload);
      const { label } = resolveBillPaymentLabel(totals);
      const payable = parseInvoiceNet(payload);
      if (payable <= 0) return [];
      return [{ provider: label, amount: payable, reference: null, status: 'Success' }];
    }

    const result: BillDetailPayment[] = [];
    for (const p of payments) {
      if (!p || typeof p !== 'object') continue;
      const row = p as Record<string, unknown>;
      const amount = readNumber(row.amount);
      if (amount <= 0) continue;
      result.push({
        provider: readString(row.provider) ?? readString(row.Provider) ?? 'Other',
        amount: roundMoney(amount),
        reference: readString(row.reference) ?? readString(row.providerReference) ?? null,
        status: readString(row.status) ?? null,
      });
    }
    return result;
  }
}
