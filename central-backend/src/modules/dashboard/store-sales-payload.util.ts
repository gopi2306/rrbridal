import type { ResolvedDateRange, StoreSalesPeriodPreset } from './store-sales-dashboard.types';
import { roundMoney } from '../../common/money.util';

const MONTH_NAMES = [
  'Jan',
  'Feb',
  'Mar',
  'Apr',
  'May',
  'Jun',
  'Jul',
  'Aug',
  'Sep',
  'Oct',
  'Nov',
  'Dec',
];

const DAY_NAMES = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

export function readNumber(value: unknown): number {
  if (value === undefined || value === null || value === '') return 0;
  const n = typeof value === 'number' ? value : Number(String(value).replace(/,/g, ''));
  return Number.isFinite(n) ? roundMoney(n) : 0;
}

export function readString(value: unknown): string | undefined {
  if (value === undefined || value === null) return undefined;
  const s = String(value).trim();
  return s === '' ? undefined : s;
}

export function parseOccurredAt(
  payload: Record<string, unknown>,
  docCreatedAt?: unknown,
): Date | null {
  const fromPayload = readString(payload.createdAtUtc) ?? readString(payload.createdAt);
  if (fromPayload) {
    const d = new Date(fromPayload);
    if (!Number.isNaN(d.getTime())) return d;
  }
  if (docCreatedAt instanceof Date && !Number.isNaN(docCreatedAt.getTime())) return docCreatedAt;
  if (typeof docCreatedAt === 'string' || typeof docCreatedAt === 'number') {
    const d = new Date(docCreatedAt);
    if (!Number.isNaN(d.getTime())) return d;
  }
  return null;
}

export function isInRange(d: Date, range: ResolvedDateRange): boolean {
  return d.getTime() >= range.from.getTime() && d.getTime() <= range.to.getTime();
}

export function formatYmd(d: Date): string {
  const y = d.getUTCFullYear();
  const m = String(d.getUTCMonth() + 1).padStart(2, '0');
  const day = String(d.getUTCDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

export function dayBucketKey(d: Date): string {
  return formatYmd(d);
}

export function monthBucketKey(d: Date): string {
  const y = d.getUTCFullYear();
  const m = String(d.getUTCMonth() + 1).padStart(2, '0');
  return `${y}-${m}`;
}

export function bucketKeyForDate(d: Date, bucketByMonth: boolean): string {
  return bucketByMonth ? monthBucketKey(d) : dayBucketKey(d);
}

export function labelForBucketKey(key: string, bucketByMonth: boolean): string {
  if (bucketByMonth) {
    const [y, m] = key.split('-');
    const monthIdx = Number(m) - 1;
    return `${MONTH_NAMES[monthIdx] ?? m} ${y}`;
  }
  const d = new Date(`${key}T12:00:00.000Z`);
  const dayName = DAY_NAMES[d.getUTCDay()] ?? '';
  const monthName = MONTH_NAMES[d.getUTCMonth()] ?? '';
  return `${dayName}, ${d.getUTCDate()} ${monthName}`;
}

export function resolveDateRange(params: {
  period: StoreSalesPeriodPreset;
  from?: string;
  to?: string;
  year: number;
  month: number;
}): ResolvedDateRange {
  const now = new Date();
  let from: Date;
  let to: Date;
  let label: string;
  let bucketByMonth = false;

  switch (params.period) {
    case 'today': {
      from = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate(), 0, 0, 0, 0));
      to = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate(), 23, 59, 59, 999));
      label = 'Today';
      break;
    }
    case 'week': {
      const day = now.getUTCDay();
      const diffToMonday = day === 0 ? 6 : day - 1;
      from = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate() - diffToMonday, 0, 0, 0, 0));
      to = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate(), 23, 59, 59, 999));
      label = 'This week';
      break;
    }
    case 'month': {
      from = new Date(Date.UTC(params.year, params.month - 1, 1, 0, 0, 0, 0));
      to = new Date(Date.UTC(params.year, params.month, 0, 23, 59, 59, 999));
      label = `${MONTH_NAMES[params.month - 1]} ${params.year}`;
      break;
    }
    case 'year': {
      from = new Date(Date.UTC(params.year, 0, 1, 0, 0, 0, 0));
      to = new Date(Date.UTC(params.year, 11, 31, 23, 59, 59, 999));
      label = String(params.year);
      bucketByMonth = true;
      break;
    }
    case 'custom':
    default: {
      if (!params.from || !params.to) {
        throw new Error('from and to are required when period=custom');
      }
      from = new Date(`${params.from}T00:00:00.000Z`);
      to = new Date(`${params.to}T23:59:59.999Z`);
      if (Number.isNaN(from.getTime()) || Number.isNaN(to.getTime())) {
        throw new Error('Invalid from or to date');
      }
      const days =
        Math.floor((to.getTime() - from.getTime()) / (24 * 60 * 60 * 1000)) + 1;
      bucketByMonth = days > 31;
      label = 'Selected range';
      break;
    }
  }

  return {
    from,
    to,
    fromYmd: formatYmd(from),
    toYmd: formatYmd(to),
    label,
    bucketByMonth,
  };
}

/** Bill total after discounts + discounts given + credit note applied at checkout. */
export function parseInvoiceGrossSale(payload: Record<string, unknown>): number {
  return (
    parseInvoiceNet(payload) +
    parseInvoiceDiscounts(payload) +
    parseInvoiceCreditApplied(payload)
  );
}

/** @deprecated Prefer parseInvoiceGrossSale — kept for callers expecting pre-discount reconstruction. */
export function parseInvoiceGross(payload: Record<string, unknown>): number {
  return parseInvoiceGrossSale(payload);
}

export function parseInvoiceNet(payload: Record<string, unknown>): number {
  return readNumber(payload.payable);
}

export function parseInvoiceDiscounts(payload: Record<string, unknown>): number {
  return readNumber(payload.itemDiscount) + readNumber(payload.cashDiscAmount);
}

export function parseInvoiceCreditApplied(payload: Record<string, unknown>): number {
  return readNumber(payload.creditApplied);
}

export function sumLineQty(lines: unknown): number {
  if (!Array.isArray(lines)) return 0;
  let sum = 0;
  for (const line of lines) {
    if (!line || typeof line !== 'object') continue;
    sum += readNumber((line as Record<string, unknown>).qty);
  }
  return sum;
}

export type PaymentModeBucket = 'Cash' | 'Card' | 'UPI' | 'Credit' | 'Other';

/** Maps WPF provider names (Cash, PineLabs, Razorpay, CreditNote) and paymentMode labels. */
export function classifyPaymentProvider(provider: string): PaymentModeBucket {
  const p = provider.trim().toLowerCase().replace(/\s+/g, '');
  if (!p) return 'Other';
  if (p === 'cash') return 'Cash';
  if (p === 'pinelabs' || p.includes('pine') || p === 'card') return 'Card';
  if (p === 'razorpay' || p.includes('razor') || p === 'upi') return 'UPI';
  if (p === 'creditnote' || p.includes('credit')) return 'Credit';
  return 'Other';
}

export function normalizePaymentMode(provider: string): string {
  return classifyPaymentProvider(provider);
}

export type PaymentTotals = {
  cash: number;
  card: number;
  upi: number;
  creditNote: number;
};

function addPaymentToTotals(totals: PaymentTotals, mode: PaymentModeBucket, amount: number) {
  switch (mode) {
    case 'Cash':
      totals.cash += amount;
      break;
    case 'Card':
      totals.card += amount;
      break;
    case 'UPI':
      totals.upi += amount;
      break;
    case 'Credit':
      totals.creditNote += amount;
      break;
    default:
      break;
  }
}

export function parsePaymentTotals(payload: Record<string, unknown>): PaymentTotals {
  const totals: PaymentTotals = { cash: 0, card: 0, upi: 0, creditNote: 0 };
  const parsed = parseInvoicePayments(payload);
  for (const pay of parsed) {
    addPaymentToTotals(totals, pay.mode as PaymentModeBucket, pay.amount);
  }

  // Legacy / sparse payloads: paymentMode + payable without payments[] legs
  if (parsed.length === 0) {
    const payable = readNumber(payload.payable);
    const mode = classifyPaymentProvider(readString(payload.paymentMode) ?? '');
    if (payable > 0 && mode !== 'Other') {
      addPaymentToTotals(totals, mode, payable);
    }
  }

  return totals;
}

function isCashRefundReturnMode(returnMode: string | undefined): boolean {
  const m = (returnMode ?? '').trim().toLowerCase().replace(/[\s-]+/g, '_');
  return m === 'cash_refund' || m === 'cashrefund';
}

/** Cash refunded to customer when returnMode is cash_refund. */
export function parseReturnCashRefund(payload: Record<string, unknown>): number {
  if (!isCashRefundReturnMode(readString(payload.returnMode))) return 0;

  const creditBalance = readNumber(payload.creditBalance);
  if (creditBalance > 0) return creditBalance;

  const returnTotal = readNumber(payload.returnTotal);
  if (returnTotal > 0) return returnTotal;

  return 0;
}

/** Exchange / top-up payments collected on a return document. */
export function parseReturnExchangePayments(payload: Record<string, unknown>): PaymentTotals {
  const amountCollected = readNumber(payload.amountCollected);
  if (amountCollected <= 0) {
    return { cash: 0, card: 0, upi: 0, creditNote: 0 };
  }
  return parsePaymentTotals(payload);
}

export function parseInvoicePayments(
  payload: Record<string, unknown>,
): Array<{ mode: string; amount: number }> {
  const payments = payload.payments;
  if (!Array.isArray(payments)) return [];
  const result: Array<{ mode: string; amount: number }> = [];
  for (const p of payments) {
    if (!p || typeof p !== 'object') continue;
    const row = p as Record<string, unknown>;
    const amount = readNumber(row.amount);
    if (amount <= 0) continue;
    const provider = readString(row.provider) ?? readString(row.Provider) ?? 'Other';
    result.push({ mode: classifyPaymentProvider(provider), amount });
  }
  return result;
}

export type LineAgg = { sku: string; description: string; qty: number };

export function parseInvoiceLines(payload: Record<string, unknown>): LineAgg[] {
  const lines = payload.lines;
  if (!Array.isArray(lines)) return [];
  const result: LineAgg[] = [];
  for (const line of lines) {
    if (!line || typeof line !== 'object') continue;
    const row = line as Record<string, unknown>;
    const qty = readNumber(row.qty);
    if (qty <= 0) continue;
    result.push({
      sku: readString(row.sku) ?? readString(row.productCode) ?? 'UNKNOWN',
      description: readString(row.description) ?? readString(row.sku) ?? 'Product',
      qty,
    });
  }
  return result;
}

export function parseReturnLineQty(payload: Record<string, unknown>): number {
  const returnLines = payload.returnLines ?? payload.lines;
  return sumLineQty(returnLines);
}

export function parseReturnLineCount(payload: Record<string, unknown>): number {
  const returnLines = payload.returnLines ?? payload.lines;
  if (!Array.isArray(returnLines)) return 0;
  return returnLines.length;
}

export type LineMarginRow = {
  sku: string;
  qty: number;
  sellingValue: number;
  costPerUnit: number;
};

function readLineSellingValue(row: Record<string, unknown>): number {
  const revisedInclusive = readNumber(row.revisedInclusiveAmount);
  if (revisedInclusive > 0) return revisedInclusive;

  const revisedTaxable = readNumber(row.revisedAmount);
  const revisedTax = readNumber(row.revisedTaxAmount);
  if (revisedTaxable > 0 || revisedTax > 0) return revisedTaxable + revisedTax;

  const lineTotal = readNumber(row.lineTotal);
  if (lineTotal > 0) return lineTotal;

  return readNumber(row.amount);
}

function readLineSku(row: Record<string, unknown>): string {
  return readString(row.sku) ?? readString(row.productCode) ?? 'UNKNOWN';
}

function parseMarginRowsFromArray(
  lines: unknown,
  options?: { returnLine?: boolean },
): LineMarginRow[] {
  if (!Array.isArray(lines)) return [];
  const result: LineMarginRow[] = [];
  for (const line of lines) {
    if (!line || typeof line !== 'object') continue;
    const row = line as Record<string, unknown>;
    const qty = options?.returnLine
      ? readNumber(row.returnQty) || readNumber(row.qty)
      : readNumber(row.qty);
    if (qty <= 0) continue;
    const sellingValue = readLineSellingValue(row);
    if (sellingValue <= 0) continue;
    result.push({
      sku: readLineSku(row),
      qty,
      sellingValue,
      costPerUnit: readNumber(row.costPrice),
    });
  }
  return result;
}

export function parseInvoiceMarginLines(payload: Record<string, unknown>): LineMarginRow[] {
  return parseMarginRowsFromArray(payload.lines);
}

export function parseReturnMarginLines(payload: Record<string, unknown>): LineMarginRow[] {
  const returnLines = payload.returnLines ?? payload.lines;
  return parseMarginRowsFromArray(returnLines, { returnLine: true });
}

export function parseExchangeMarginLines(payload: Record<string, unknown>): LineMarginRow[] {
  return parseMarginRowsFromArray(payload.exchangeLines);
}

export function computeMarginPercentage(salesMargin: number, totalCostValue: number): number {
  if (!Number.isFinite(salesMargin) || !Number.isFinite(totalCostValue) || totalCostValue <= 0) {
    return 0;
  }
  return roundMoney((salesMargin / totalCostValue) * 100);
}

export type MarginTotals = {
  totalCostValue: number;
  totalSellingValue: number;
  salesMargin: number;
  marginPercentage: number;
};

export function aggregateMarginLines(
  lines: readonly LineMarginRow[],
  costBySku: ReadonlyMap<string, number>,
  sign: 1 | -1 = 1,
): MarginTotals {
  let totalCostValue = 0;
  let totalSellingValue = 0;
  for (const row of lines) {
    const costUnit = row.costPerUnit > 0 ? row.costPerUnit : (costBySku.get(row.sku) ?? 0);
    totalCostValue += sign * costUnit * row.qty;
    totalSellingValue += sign * row.sellingValue;
  }
  const salesMargin = totalSellingValue - totalCostValue;
  return {
    totalCostValue: roundMoney(totalCostValue),
    totalSellingValue: roundMoney(totalSellingValue),
    salesMargin: roundMoney(salesMargin),
    marginPercentage: computeMarginPercentage(salesMargin, totalCostValue),
  };
}
