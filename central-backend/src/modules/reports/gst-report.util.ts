import { roundMoney } from '../../common/money.util';
import { readNumber, readString } from '../dashboard/store-sales-payload.util';
import type { ResolvedDateRange } from '../dashboard/store-sales-dashboard.types';

export type GstLineContribution = {
  hsn: string;
  gstPercent: number;
  qty: number;
  taxableAmount: number;
  taxAmount: number;
  totalInclusive: number;
};

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
  const inclusive =
    readNumber(row.revisedInclusiveAmount) ||
    readNumber(row.lineTotal);
  if (inclusive > 0) return inclusive;
  return roundMoney(taxable + taxAmount);
}

function readGstPercent(row: Record<string, unknown>): number {
  const taxPercent = readNumber(row.taxPercent);
  if (taxPercent > 0) return taxPercent;

  const igst = readNumber(row.igstPercent);
  if (igst > 0) return igst;

  const cgst = readNumber(row.cgstPercent);
  const sgst = readNumber(row.sgstPercent);
  if (cgst > 0 || sgst > 0) return roundMoney(cgst + sgst);

  return 0;
}

function readLineQty(row: Record<string, unknown>, returnLine = false): number {
  if (returnLine) return readNumber(row.returnQty) || readNumber(row.qty);
  return readNumber(row.qty);
}

function parseGstLine(
  row: Record<string, unknown>,
  options: { returnLine?: boolean; hsn?: string; sign?: 1 | -1 },
): GstLineContribution | null {
  const qty = readLineQty(row, options.returnLine);
  if (qty <= 0) return null;

  const taxAmount = readTaxAmount(row);
  const taxableAmount = readTaxableAmount(row, taxAmount);
  if (taxableAmount <= 0 && taxAmount <= 0) return null;

  const sign = options.sign ?? 1;
  const hsn = options.hsn ?? readString(row.hsn) ?? 'UNKNOWN';
  const gstPercent = readGstPercent(row);
  const totalInclusive = readInclusiveAmount(taxableAmount, taxAmount, row);

  return {
    hsn,
    gstPercent,
    qty: sign * qty,
    taxableAmount: roundMoney(sign * taxableAmount),
    taxAmount: roundMoney(sign * taxAmount),
    totalInclusive: roundMoney(sign * totalInclusive),
  };
}

export function parseInvoiceGstLines(payload: Record<string, unknown>): GstLineContribution[] {
  const lines = payload.lines;
  if (!Array.isArray(lines)) return [];

  const result: GstLineContribution[] = [];
  for (const line of lines) {
    if (!line || typeof line !== 'object') continue;
    const parsed = parseGstLine(line as Record<string, unknown>, { sign: 1 });
    if (parsed) result.push(parsed);
  }
  return result;
}

export function parseReturnGstLines(payload: Record<string, unknown>): GstLineContribution[] {
  const raw = payload.returnLines ?? payload.lines;
  if (!Array.isArray(raw)) return [];

  const result: GstLineContribution[] = [];
  for (const line of raw) {
    if (!line || typeof line !== 'object') continue;
    const parsed = parseGstLine(line as Record<string, unknown>, { returnLine: true, sign: -1 });
    if (parsed) result.push(parsed);
  }
  return result;
}

export function parseExchangeGstLines(payload: Record<string, unknown>): GstLineContribution[] {
  const raw = payload.exchangeLines;
  if (!Array.isArray(raw)) return [];

  const result: GstLineContribution[] = [];
  for (const line of raw) {
    if (!line || typeof line !== 'object') continue;
    const parsed = parseGstLine(line as Record<string, unknown>, { sign: 1 });
    if (parsed) result.push(parsed);
  }
  return result;
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
    existing.taxableAmount = roundMoney(existing.taxableAmount + row.taxableAmount);
    existing.taxAmount = roundMoney(existing.taxAmount + row.taxAmount);
    existing.totalInclusive = roundMoney(existing.totalInclusive + row.totalInclusive);
  }
  return [...byKey.values()];
}

export function aggregateByGstRate(rows: GstLineContribution[]): Array<{
  gstPercent: number;
  taxableAmount: number;
  taxAmount: number;
  totalInclusive: number;
}> {
  const byRate = new Map<number, { taxableAmount: number; taxAmount: number; totalInclusive: number }>();
  for (const row of rows) {
    const bucket = byRate.get(row.gstPercent) ?? { taxableAmount: 0, taxAmount: 0, totalInclusive: 0 };
    bucket.taxableAmount = roundMoney(bucket.taxableAmount + row.taxableAmount);
    bucket.taxAmount = roundMoney(bucket.taxAmount + row.taxAmount);
    bucket.totalInclusive = roundMoney(bucket.totalInclusive + row.totalInclusive);
    byRate.set(row.gstPercent, bucket);
  }
  return [...byRate.entries()]
    .map(([gstPercent, totals]) => ({ gstPercent, ...totals }))
    .sort((a, b) => a.gstPercent - b.gstPercent);
}

export function summarizeGstRows(
  rows: GstLineContribution[],
  documentCount: number,
): {
  summary: { taxableAmount: number; taxAmount: number; totalInclusive: number; documentCount: number };
  byGstRate: ReturnType<typeof aggregateByGstRate>;
  byHsn: Array<{
    hsn: string;
    gstPercent: number;
    qty: number;
    taxableAmount: number;
    taxAmount: number;
    totalInclusive: number;
  }>;
} {
  const merged = mergeGstContributions(rows);
  const summary = merged.reduce(
    (acc, row) => {
      acc.taxableAmount = roundMoney(acc.taxableAmount + row.taxableAmount);
      acc.taxAmount = roundMoney(acc.taxAmount + row.taxAmount);
      acc.totalInclusive = roundMoney(acc.totalInclusive + row.totalInclusive);
      return acc;
    },
    { taxableAmount: 0, taxAmount: 0, totalInclusive: 0, documentCount },
  );

  return {
    summary,
    byGstRate: aggregateByGstRate(merged),
    byHsn: merged
      .map((row) => ({
        hsn: row.hsn,
        gstPercent: row.gstPercent,
        qty: row.qty,
        taxableAmount: row.taxableAmount,
        taxAmount: row.taxAmount,
        totalInclusive: row.totalInclusive,
      }))
      .sort((a, b) => a.hsn.localeCompare(b.hsn) || a.gstPercent - b.gstPercent),
  };
}
