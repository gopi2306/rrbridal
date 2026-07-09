import { roundMoney } from '../../common/money.util';
import { readNumber, readString } from '../dashboard/store-sales-payload.util';
import type { ResolvedDateRange } from '../dashboard/store-sales-dashboard.types';
import type {
  GstHsnRow,
  GstItemRow,
  GstPurchaseInvoiceRow,
  GstRateSummaryRow,
  GstSalesInvoiceRow,
  GstSectionSummary,
  GstTaxBreakdown,
} from './gst-report.types';

export type GstDocumentType = 'invoice' | 'return' | 'exchange' | 'purchase';

export type GstLineContribution = GstTaxBreakdown & {
  hsn: string;
  qty: number;
  sku: string;
  itemName?: string;
  documentKey: string;
  documentType: GstDocumentType;
  documentNo: string;
  documentDate: string;
  storeId?: string;
  customerName?: string;
  isInterState?: boolean;
  grnNumber?: string;
  receiptNo?: string;
  poNo?: string;
  invoiceNo?: string;
  invoiceDate?: string;
  supplierName?: string;
  supplierGstNumber?: string;
};

export type GstDocumentContext = {
  documentKey: string;
  documentType: GstDocumentType;
  documentNo: string;
  documentDate: string;
  storeId?: string;
  customerName?: string;
  isInterState?: boolean;
  grnNumber?: string;
  receiptNo?: string;
  poNo?: string;
  invoiceNo?: string;
  invoiceDate?: string;
  supplierName?: string;
  supplierGstNumber?: string;
};

type TaxComponentParse = {
  gstPercent: number;
  sgstPercent: number;
  cgstPercent: number;
  igstPercent: number;
  taxAmount: number;
  sgstAmount: number;
  cgstAmount: number;
  igstAmount: number;
};

type MonetaryTotals = Pick<
  GstTaxBreakdown,
  'taxableAmount' | 'taxAmount' | 'sgstAmount' | 'cgstAmount' | 'igstAmount' | 'totalInclusive'
>;

export function buildSalePayloadTimeFilter(
  range: ResolvedDateRange,
  storeId?: string,
): Record<string, unknown> {
  const fromIso = range.from.toISOString();
  const toIso = range.to.toISOString();
  const timeOr = [
    { 'payload.createdAtUtc': { $gte: fromIso, $lte: toIso } },
    {
      $and: [
        {
          $or: [
            { 'payload.createdAtUtc': { $exists: false } },
            { 'payload.createdAtUtc': null },
            { 'payload.createdAtUtc': '' },
          ],
        },
        { createdAt: { $gte: range.from, $lte: range.to } },
      ],
    },
  ];
  if (storeId?.trim()) {
    return { storeId: storeId.trim(), $or: timeOr };
  }
  return { $or: timeOr };
}

export function formatDocumentDateYmd(value: unknown, fallback?: Date): string {
  if (typeof value === 'string' && value.trim()) {
    const d = new Date(value);
    if (!Number.isNaN(d.getTime())) return d.toISOString().slice(0, 10);
    const ddmmyyyy = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(value.trim());
    if (ddmmyyyy) {
      return `${ddmmyyyy[3]}-${ddmmyyyy[2]}-${ddmmyyyy[1]}`;
    }
    if (/^\d{4}-\d{2}-\d{2}/.test(value.trim())) return value.trim().slice(0, 10);
  }
  if (fallback && !Number.isNaN(fallback.getTime())) {
    return fallback.toISOString().slice(0, 10);
  }
  return '';
}

function readTaxAmount(row: Record<string, unknown>): number {
  const revisedTax = readNumber(row.revisedTaxAmount);
  if (revisedTax > 0) return revisedTax;

  const taxAmt = readNumber(row.taxAmount) || readNumber(row.taxAmt);
  if (taxAmt > 0) return taxAmt;

  const cgst = readNumber(row.cgstAmount) || readNumber(row.cgstAmt);
  const sgst = readNumber(row.sgstAmount) || readNumber(row.sgstAmt);
  const igst = readNumber(row.igstAmount) || readNumber(row.igstAmt);
  return roundMoney(cgst + sgst + igst);
}

function readTaxComponents(row: Record<string, unknown>): TaxComponentParse {
  const cgstPct = readNumber(row.cgstPercent);
  const sgstPct = readNumber(row.sgstPercent);
  const igstPct = readNumber(row.igstPercent);
  const taxPercent = readNumber(row.taxPercent);

  let gstPercent = taxPercent;
  if (gstPercent <= 0 && igstPct > 0) gstPercent = igstPct;
  if (gstPercent <= 0 && (cgstPct > 0 || sgstPct > 0)) gstPercent = roundMoney(cgstPct + sgstPct);

  let sgstAmount = roundMoney(readNumber(row.sgstAmount) || readNumber(row.sgstAmt));
  let cgstAmount = roundMoney(readNumber(row.cgstAmount) || readNumber(row.cgstAmt));
  let igstAmount = roundMoney(readNumber(row.igstAmount) || readNumber(row.igstAmt));
  let taxAmount = readTaxAmount(row);

  if (sgstAmount === 0 && cgstAmount === 0 && igstAmount === 0 && taxAmount > 0) {
    if (igstPct > 0) {
      igstAmount = taxAmount;
    } else {
      sgstAmount = roundMoney(taxAmount / 2);
      cgstAmount = roundMoney(taxAmount - sgstAmount);
    }
  } else if (taxAmount <= 0) {
    taxAmount = roundMoney(sgstAmount + cgstAmount + igstAmount);
  }

  let sgstPercent = sgstPct;
  let cgstPercent = cgstPct;
  let igstPercent = igstPct;
  if (igstAmount > 0 && sgstAmount === 0 && cgstAmount === 0) {
    if (igstPercent <= 0) igstPercent = gstPercent;
    sgstPercent = 0;
    cgstPercent = 0;
  } else if (gstPercent > 0) {
    if (sgstPercent <= 0 && cgstPercent <= 0) {
      sgstPercent = gstPercent / 2;
      cgstPercent = gstPercent / 2;
    }
    igstPercent = 0;
  }

  return {
    gstPercent,
    sgstPercent,
    cgstPercent,
    igstPercent,
    taxAmount,
    sgstAmount,
    cgstAmount,
    igstAmount,
  };
}

function readTaxableAmount(row: Record<string, unknown>, taxAmount: number): number {
  const revised = readNumber(row.revisedAmount);
  if (revised > 0) return revised;

  const amount = readNumber(row.amount);
  if (amount > 0) return amount;

  const inclusive =
    readNumber(row.revisedInclusiveAmount) ||
    readNumber(row.lineTotal) ||
    readNumber(row.revisedInclusiveAmount);
  if (inclusive > 0 && taxAmount > 0) return roundMoney(inclusive - taxAmount);
  if (inclusive > 0) return inclusive;

  const rate = readNumber(row.rate);
  const qty = readNumber(row.qty) || readNumber(row.returnQty);
  if (rate > 0 && qty > 0) return roundMoney(rate * qty);

  return 0;
}

function readInclusiveAmount(taxable: number, taxAmount: number, row: Record<string, unknown>): number {
  const inclusive = readNumber(row.revisedInclusiveAmount) || readNumber(row.lineTotal);
  if (inclusive > 0) return inclusive;
  return roundMoney(taxable + taxAmount);
}

function readLineQty(row: Record<string, unknown>, returnLine = false): number {
  if (returnLine) return readNumber(row.returnQty) || readNumber(row.qty);
  return readNumber(row.qty);
}

function readLineSku(row: Record<string, unknown>): string {
  return readString(row.sku) ?? readString(row.productCode) ?? '';
}

function readLineItemName(row: Record<string, unknown>): string | undefined {
  const name = readString(row.description) ?? readString(row.itemName);
  return name?.trim() || undefined;
}

function applySignToMonetary(
  totals: MonetaryTotals,
  sign: 1 | -1,
): MonetaryTotals {
  return {
    taxableAmount: roundMoney(sign * totals.taxableAmount),
    taxAmount: roundMoney(sign * totals.taxAmount),
    sgstAmount: roundMoney(sign * totals.sgstAmount),
    cgstAmount: roundMoney(sign * totals.cgstAmount),
    igstAmount: roundMoney(sign * totals.igstAmount),
    totalInclusive: roundMoney(sign * totals.totalInclusive),
  };
}

export function deriveTaxPercents(
  totals: MonetaryTotals,
  gstPercentHint = 0,
): Pick<GstTaxBreakdown, 'gstPercent' | 'sgstPercent' | 'cgstPercent' | 'igstPercent'> {
  if (totals.igstAmount > 0 && totals.sgstAmount === 0 && totals.cgstAmount === 0) {
    const igstPercent =
      gstPercentHint > 0
        ? gstPercentHint
        : totals.taxableAmount > 0
          ? roundMoney((totals.igstAmount / totals.taxableAmount) * 100)
          : 0;
    return { gstPercent: igstPercent, sgstPercent: 0, cgstPercent: 0, igstPercent };
  }

  const gstPercent =
    gstPercentHint > 0
      ? gstPercentHint
      : totals.taxableAmount > 0
        ? roundMoney((totals.taxAmount / totals.taxableAmount) * 100)
        : 0;
  return {
    gstPercent,
    sgstPercent: gstPercent / 2,
    cgstPercent: gstPercent / 2,
    igstPercent: 0,
  };
}

export function toTaxBreakdown(
  totals: MonetaryTotals,
  gstPercentHint = 0,
): GstTaxBreakdown {
  return {
    ...deriveTaxPercents(totals, gstPercentHint),
    ...totals,
  };
}

function addMonetary(target: MonetaryTotals, source: MonetaryTotals): void {
  target.taxableAmount = roundMoney(target.taxableAmount + source.taxableAmount);
  target.taxAmount = roundMoney(target.taxAmount + source.taxAmount);
  target.sgstAmount = roundMoney(target.sgstAmount + source.sgstAmount);
  target.cgstAmount = roundMoney(target.cgstAmount + source.cgstAmount);
  target.igstAmount = roundMoney(target.igstAmount + source.igstAmount);
  target.totalInclusive = roundMoney(target.totalInclusive + source.totalInclusive);
}

function emptyMonetaryTotals(): MonetaryTotals {
  return {
    taxableAmount: 0,
    taxAmount: 0,
    sgstAmount: 0,
    cgstAmount: 0,
    igstAmount: 0,
    totalInclusive: 0,
  };
}

function parseGstLine(
  row: Record<string, unknown>,
  options: {
    returnLine?: boolean;
    hsn?: string;
    sign?: 1 | -1;
    document?: GstDocumentContext;
  },
): GstLineContribution | null {
  const qty = readLineQty(row, options.returnLine);
  if (qty <= 0) return null;

  const tax = readTaxComponents(row);
  const taxableAmount = readTaxableAmount(row, tax.taxAmount);
  if (taxableAmount <= 0 && tax.taxAmount <= 0) return null;

  const sign = options.sign ?? 1;
  const hsn = options.hsn ?? readString(row.hsn) ?? 'UNKNOWN';
  const sku = readLineSku(row);
  const itemName = readLineItemName(row);
  const totalInclusive = readInclusiveAmount(taxableAmount, tax.taxAmount, row);
  const monetary = applySignToMonetary(
    {
      taxableAmount,
      taxAmount: tax.taxAmount,
      sgstAmount: tax.sgstAmount,
      cgstAmount: tax.cgstAmount,
      igstAmount: tax.igstAmount,
      totalInclusive,
    },
    sign,
  );

  const doc = options.document;
  const line: GstLineContribution = {
    hsn,
    sku,
    qty: sign * qty,
    gstPercent: tax.gstPercent,
    sgstPercent: tax.sgstPercent,
    cgstPercent: tax.cgstPercent,
    igstPercent: tax.igstPercent,
    ...monetary,
    documentKey: doc?.documentKey ?? '',
    documentType: doc?.documentType ?? 'invoice',
    documentNo: doc?.documentNo ?? '',
    documentDate: doc?.documentDate ?? '',
  };
  if (itemName) line.itemName = itemName;
  if (doc?.storeId) line.storeId = doc.storeId;
  if (doc?.customerName) line.customerName = doc.customerName;
  if (doc?.isInterState) line.isInterState = doc.isInterState;
  if (doc?.grnNumber) line.grnNumber = doc.grnNumber;
  if (doc?.receiptNo) line.receiptNo = doc.receiptNo;
  if (doc?.poNo) line.poNo = doc.poNo;
  if (doc?.invoiceNo) line.invoiceNo = doc.invoiceNo;
  if (doc?.invoiceDate) line.invoiceDate = doc.invoiceDate;
  if (doc?.supplierName) line.supplierName = doc.supplierName;
  if (doc?.supplierGstNumber) line.supplierGstNumber = doc.supplierGstNumber;
  return line;
}

export function parseInvoiceGstLines(
  payload: Record<string, unknown>,
  document: GstDocumentContext,
): GstLineContribution[] {
  const lines = payload.lines;
  if (!Array.isArray(lines)) return [];

  const result: GstLineContribution[] = [];
  for (const line of lines) {
    if (!line || typeof line !== 'object') continue;
    const parsed = parseGstLine(line as Record<string, unknown>, { sign: 1, document });
    if (parsed) result.push(parsed);
  }
  return result;
}

export function parseReturnGstLines(
  payload: Record<string, unknown>,
  document: GstDocumentContext,
): GstLineContribution[] {
  const raw = payload.returnLines ?? payload.lines;
  if (!Array.isArray(raw)) return [];

  const result: GstLineContribution[] = [];
  for (const line of raw) {
    if (!line || typeof line !== 'object') continue;
    const parsed = parseGstLine(line as Record<string, unknown>, {
      returnLine: true,
      sign: -1,
      document,
    });
    if (parsed) result.push(parsed);
  }
  return result;
}

export function parseExchangeGstLines(
  payload: Record<string, unknown>,
  document: GstDocumentContext,
): GstLineContribution[] {
  const raw = payload.exchangeLines;
  if (!Array.isArray(raw)) return [];

  const result: GstLineContribution[] = [];
  for (const line of raw) {
    if (!line || typeof line !== 'object') continue;
    const parsed = parseGstLine(line as Record<string, unknown>, { sign: 1, document });
    if (parsed) result.push(parsed);
  }
  return result;
}

export function buildPurchaseTaxComponents(
  taxable: number,
  taxAmount: number,
  poLine?: {
    taxPercent?: number;
    cgstPercent?: number;
    sgstPercent?: number;
    cgstAmount?: number;
    sgstAmount?: number;
  },
  gstPercentFallback = 0,
): TaxComponentParse {
  let gstPercent = poLine?.taxPercent ?? gstPercentFallback;
  let cgstAmount = roundMoney(poLine?.cgstAmount ?? 0);
  let sgstAmount = roundMoney(poLine?.sgstAmount ?? 0);
  const igstAmount = 0;

  if (cgstAmount === 0 && sgstAmount === 0 && taxAmount > 0) {
    sgstAmount = roundMoney(taxAmount / 2);
    cgstAmount = roundMoney(taxAmount - sgstAmount);
  } else if (taxAmount <= 0) {
    taxAmount = roundMoney(cgstAmount + sgstAmount);
  }

  let cgstPercent = poLine?.cgstPercent ?? 0;
  let sgstPercent = poLine?.sgstPercent ?? 0;
  if (gstPercent <= 0 && (cgstPercent > 0 || sgstPercent > 0)) {
    gstPercent = roundMoney(cgstPercent + sgstPercent);
  }
  if (gstPercent > 0 && cgstPercent <= 0 && sgstPercent <= 0) {
    cgstPercent = gstPercent / 2;
    sgstPercent = gstPercent / 2;
  }

  return {
    gstPercent,
    sgstPercent,
    cgstPercent,
    igstPercent: 0,
    taxAmount,
    sgstAmount,
    cgstAmount,
    igstAmount,
  };
}

export function mergeGstContributions(rows: GstLineContribution[]): GstLineContribution[] {
  const byKey = new Map<string, GstLineContribution>();
  for (const row of rows) {
    const hsn = row.hsn.trim() || 'UNKNOWN';
    const key = `${hsn.toLowerCase()}|${row.gstPercent}`;
    const existing = byKey.get(key);
    if (!existing) {
      byKey.set(key, { ...row, hsn });
      continue;
    }
    existing.qty = roundMoney(existing.qty + row.qty);
    addMonetary(existing, row);
  }
  return [...byKey.values()];
}

export function aggregateByGstRate(rows: GstLineContribution[]): GstRateSummaryRow[] {
  const byRate = new Map<number, MonetaryTotals>();
  for (const row of rows) {
    const bucket = byRate.get(row.gstPercent) ?? emptyMonetaryTotals();
    addMonetary(bucket, row);
    byRate.set(row.gstPercent, bucket);
  }
  return [...byRate.entries()]
    .map(([gstPercent, totals]) => toTaxBreakdown(totals, gstPercent))
    .sort((a, b) => a.gstPercent - b.gstPercent);
}

export function aggregateByItem(
  rows: GstLineContribution[],
  productNames?: Map<string, string>,
): GstItemRow[] {
  const bySku = new Map<string, GstItemRow>();
  for (const row of rows) {
    const sku = row.sku.trim();
    if (!sku) continue;
    const key = sku.toLowerCase();
    const existing = bySku.get(key);
    if (!existing) {
      const itemRow: GstItemRow = {
        sku,
        qty: row.qty,
        ...toTaxBreakdown(row, row.gstPercent),
      };
      const itemName = row.itemName ?? productNames?.get(sku);
      if (itemName) itemRow.itemName = itemName;
      if (row.hsn !== 'UNKNOWN') itemRow.hsn = row.hsn;
      bySku.set(key, itemRow);
      continue;
    }
    existing.qty = roundMoney(existing.qty + row.qty);
    addMonetary(existing, row);
    if (!existing.itemName && row.itemName) existing.itemName = row.itemName;
    if (!existing.itemName) {
      const fromProduct = productNames?.get(sku);
      if (fromProduct) existing.itemName = fromProduct;
    }
    if (!existing.hsn && row.hsn !== 'UNKNOWN') existing.hsn = row.hsn;
    const percents = deriveTaxPercents(existing, existing.gstPercent || row.gstPercent);
    Object.assign(existing, percents);
  }
  return [...bySku.values()].sort((a, b) => a.sku.localeCompare(b.sku));
}

export function aggregateSalesByInvoice(rows: GstLineContribution[]): GstSalesInvoiceRow[] {
  const byDoc = new Map<string, GstSalesInvoiceRow & { lineCount: number }>();
  for (const row of rows) {
    if (!row.documentKey) continue;
    const existing = byDoc.get(row.documentKey);
    if (!existing) {
      const invoiceRow: GstSalesInvoiceRow & { lineCount: number } = {
        storeId: row.storeId ?? '',
        documentType:
          row.documentType === 'return' || row.documentType === 'exchange'
            ? row.documentType
            : 'invoice',
        documentNo: row.documentNo,
        documentDate: row.documentDate,
        lineCount: 1,
        ...toTaxBreakdown(row, row.gstPercent),
      };
      if (row.customerName) invoiceRow.customerName = row.customerName;
      if (row.isInterState) invoiceRow.isInterState = row.isInterState;
      byDoc.set(row.documentKey, invoiceRow);
      continue;
    }
    existing.lineCount += 1;
    addMonetary(existing, row);
    const percents = deriveTaxPercents(existing, existing.gstPercent || row.gstPercent);
    Object.assign(existing, percents);
  }
  return [...byDoc.values()].sort(
    (a, b) => a.documentDate.localeCompare(b.documentDate) || a.documentNo.localeCompare(b.documentNo),
  );
}

export function aggregatePurchaseByInvoice(rows: GstLineContribution[]): GstPurchaseInvoiceRow[] {
  const byDoc = new Map<string, GstPurchaseInvoiceRow>();
  for (const row of rows) {
    if (!row.documentKey) continue;
    const existing = byDoc.get(row.documentKey);
    if (!existing) {
      const invoiceRow: GstPurchaseInvoiceRow = {
        receiptNo: row.receiptNo ?? row.documentNo,
        receivedQty: row.qty,
        purchaseCost: row.taxableAmount,
        discountAmount: 0,
        ...toTaxBreakdown(row, row.gstPercent),
      };
      if (row.grnNumber) invoiceRow.grnNumber = row.grnNumber;
      if (row.poNo) invoiceRow.poNo = row.poNo;
      if (row.invoiceNo) invoiceRow.invoiceNo = row.invoiceNo;
      if (row.invoiceDate) invoiceRow.invoiceDate = row.invoiceDate;
      if (row.supplierName) invoiceRow.supplierName = row.supplierName;
      if (row.supplierGstNumber) invoiceRow.supplierGstNumber = row.supplierGstNumber;
      byDoc.set(row.documentKey, invoiceRow);
      continue;
    }
    existing.receivedQty = roundMoney(existing.receivedQty + row.qty);
    existing.purchaseCost = roundMoney((existing.purchaseCost ?? 0) + row.taxableAmount);
    addMonetary(existing, row);
    const percents = deriveTaxPercents(existing, existing.gstPercent || row.gstPercent);
    Object.assign(existing, percents);
  }
  return [...byDoc.values()].sort(
    (a, b) =>
      (a.invoiceDate ?? '').localeCompare(b.invoiceDate ?? '') ||
      a.receiptNo.localeCompare(b.receiptNo),
  );
}

export function summarizeGstRows(
  rows: GstLineContribution[],
  documentCount: number,
  options?: { productNames?: Map<string, string>; section: 'sales' | 'purchase' },
): {
  summary: GstSectionSummary;
  byGstRate: GstRateSummaryRow[];
  byHsn: GstHsnRow[];
  byItem: GstItemRow[];
  byInvoice: GstSalesInvoiceRow[] | GstPurchaseInvoiceRow[];
} {
  const merged = mergeGstContributions(rows);
  const summaryTotals = merged.reduce((acc, row) => {
    addMonetary(acc, row);
    return acc;
  }, emptyMonetaryTotals());

  const summary: GstSectionSummary = {
    ...toTaxBreakdown(summaryTotals),
    documentCount,
  };

  const byHsn: GstHsnRow[] = merged
    .map((row) => ({
      hsn: row.hsn,
      qty: row.qty,
      ...toTaxBreakdown(row, row.gstPercent),
    }))
    .sort((a, b) => a.hsn.localeCompare(b.hsn) || a.gstPercent - b.gstPercent);

  const byInvoice =
    options?.section === 'purchase'
      ? aggregatePurchaseByInvoice(rows)
      : aggregateSalesByInvoice(rows);

  return {
    summary,
    byGstRate: aggregateByGstRate(merged),
    byHsn,
    byItem: aggregateByItem(rows, options?.productNames),
    byInvoice,
  };
}
