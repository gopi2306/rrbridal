import type {
  ResolvedDateRange,
  StoreSalesDashboardPeriod,
  StoreSalesPeriodPreset,
} from './store-sales-dashboard.types';
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

/** India Standard Time — aligns central dashboard with WPF local day close. */
export const BUSINESS_TZ_IANA = 'Asia/Kolkata';
const BUSINESS_TZ_OFFSET_MS = 5.5 * 60 * 60 * 1000;

function businessNow(): Date {
  return new Date(Date.now() + BUSINESS_TZ_OFFSET_MS);
}

function businessCalendarParts(d: Date): { y: number; m: number; day: number; dow: number } {
  return {
    y: d.getUTCFullYear(),
    m: d.getUTCMonth(),
    day: d.getUTCDate(),
    dow: d.getUTCDay(),
  };
}

/** Calendar date parts in IST (UTC+5:30) for any instant. */
export function toBusinessCalendarParts(d: Date): { y: number; m: number; day: number; dow: number } {
  return businessCalendarParts(new Date(d.getTime() + BUSINESS_TZ_OFFSET_MS));
}

/** Today in IST for default year/month query params. */
export function businessTodayParts(): { year: number; month: number } {
  const { y, m } = businessCalendarParts(businessNow());
  return { year: y, month: m + 1 };
}

function parseYmdParts(ymd: string): { y: number; m: number; day: number } | null {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(ymd.trim());
  if (!m) return null;
  return { y: Number(m[1]), m: Number(m[2]) - 1, day: Number(m[3]) };
}

/** IST calendar date as `YYYY-MM-DD` (month is 0-indexed). */
export function formatYmdFromParts(y: number, m: number, day: number): string {
  return `${y}-${String(m + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
}

/**
 * IST business-day bounds as UTC instants for MongoDB / ISO timestamp comparison.
 * `from` = IST 00:00:00, `to` = IST 23:59:59.999 on the given calendar date.
 */
export function businessDayBoundsUtc(y: number, m: number, day: number): { from: Date; to: Date } {
  const from = new Date(Date.UTC(y, m, day, 0, 0, 0, 0) - BUSINESS_TZ_OFFSET_MS);
  const to = new Date(Date.UTC(y, m, day, 23, 59, 59, 999) - BUSINESS_TZ_OFFSET_MS);
  return { from, to };
}

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

/** `YYYY-MM-DD` in IST — use for API period labels and business-day buckets. */
export function formatBusinessYmd(d: Date): string {
  const { y, m, day } = toBusinessCalendarParts(d);
  return `${y}-${String(m + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
}

export function dayBucketKey(d: Date): string {
  return formatBusinessYmd(d);
}

export function monthBucketKey(d: Date): string {
  const { y, m } = toBusinessCalendarParts(d);
  return `${y}-${String(m + 1).padStart(2, '0')}`;
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
  const parts = parseYmdParts(key);
  if (!parts) return key;
  const dow = new Date(Date.UTC(parts.y, parts.m, parts.day, 12, 0, 0, 0)).getUTCDay();
  const dayName = DAY_NAMES[dow] ?? '';
  const monthName = MONTH_NAMES[parts.m] ?? '';
  return `${dayName}, ${parts.day} ${monthName}`;
}

export function resolveDateRange(params: {
  period: StoreSalesPeriodPreset;
  from?: string;
  to?: string;
  year: number;
  month: number;
}): ResolvedDateRange {
  const biz = businessCalendarParts(businessNow());
  let from: Date;
  let to: Date;
  let fromYmd: string;
  let toYmd: string;
  let label: string;
  let bucketByMonth = false;

  switch (params.period) {
    case 'today': {
      ({ from, to } = businessDayBoundsUtc(biz.y, biz.m, biz.day));
      fromYmd = formatYmdFromParts(biz.y, biz.m, biz.day);
      toYmd = fromYmd;
      label = 'Today';
      break;
    }
    case 'week': {
      const diffToMonday = biz.dow === 0 ? 6 : biz.dow - 1;
      const monday = new Date(Date.UTC(biz.y, biz.m, biz.day - diffToMonday));
      const mon = businessCalendarParts(monday);
      ({ from } = businessDayBoundsUtc(mon.y, mon.m, mon.day));
      ({ to } = businessDayBoundsUtc(biz.y, biz.m, biz.day));
      fromYmd = formatYmdFromParts(mon.y, mon.m, mon.day);
      toYmd = formatYmdFromParts(biz.y, biz.m, biz.day);
      label = 'This week';
      break;
    }
    case 'month': {
      const lastDay = new Date(Date.UTC(params.year, params.month, 0)).getUTCDate();
      ({ from } = businessDayBoundsUtc(params.year, params.month - 1, 1));
      ({ to } = businessDayBoundsUtc(params.year, params.month - 1, lastDay));
      fromYmd = formatYmdFromParts(params.year, params.month - 1, 1);
      toYmd = formatYmdFromParts(params.year, params.month - 1, lastDay);
      label = `${MONTH_NAMES[params.month - 1]} ${params.year}`;
      break;
    }
    case 'year': {
      ({ from } = businessDayBoundsUtc(params.year, 0, 1));
      ({ to } = businessDayBoundsUtc(params.year, 11, 31));
      fromYmd = formatYmdFromParts(params.year, 0, 1);
      toYmd = formatYmdFromParts(params.year, 11, 31);
      label = String(params.year);
      bucketByMonth = true;
      break;
    }
    case 'custom':
    default: {
      if (!params.from || !params.to) {
        throw new Error('from and to are required when period=custom');
      }
      const fromParts = parseYmdParts(params.from);
      const toParts = parseYmdParts(params.to);
      if (!fromParts || !toParts) {
        throw new Error('Invalid from or to date');
      }
      fromYmd = params.from.trim();
      toYmd = params.to.trim();
      ({ from } = businessDayBoundsUtc(fromParts.y, fromParts.m, fromParts.day));
      ({ to } = businessDayBoundsUtc(toParts.y, toParts.m, toParts.day));
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
    fromYmd,
    toYmd,
    label,
    bucketByMonth,
    timezone: BUSINESS_TZ_IANA,
  };
}

/** Mongo filter: UTC `createdAt` on synced store-sale documents (IST range → UTC bounds). */
export function buildMongoCreatedAtFilter(range: ResolvedDateRange): {
  createdAt: { $gte: Date; $lte: Date };
} {
  return { createdAt: { $gte: range.from, $lte: range.to } };
}

/**
 * Mongo filter for invoices/returns where business time is `payload.createdAtUtc` (ISO UTC string).
 * Falls back to document `createdAt` when payload timestamp is missing.
 */
export function buildDashboardPeriod(
  preset: StoreSalesPeriodPreset,
  range: ResolvedDateRange,
): StoreSalesDashboardPeriod {
  return {
    preset,
    from: range.fromYmd,
    to: range.toYmd,
    label: range.label,
    timezone: range.timezone,
  };
}

export function buildStoreSalePayloadTimeFilter(
  storeId: string,
  range: ResolvedDateRange,
): Record<string, unknown> {
  return buildSalesPayloadTimeFilter({ storeIds: [storeId], range });
}

/** Multi-store (or all-store) invoice/return filter by business period. */
export function buildSalesPayloadTimeFilter(params: {
  storeIds?: readonly string[];
  range: ResolvedDateRange;
}): Record<string, unknown> {
  const fromIso = params.range.from.toISOString();
  const toIso = params.range.to.toISOString();
  const timeOr: Record<string, unknown>[] = [
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
        { createdAt: { $gte: params.range.from, $lte: params.range.to } },
      ],
    },
  ];

  const filter: Record<string, unknown> = { $or: timeOr };

  const storeIds = params.storeIds
    ?.map((id) => id.trim().toLowerCase())
    .filter((id) => id.length > 0);
  if (storeIds && storeIds.length === 1) {
    filter.storeId = storeIds[0];
  } else if (storeIds && storeIds.length > 1) {
    filter.storeId = { $in: storeIds };
  }

  return filter;
}

/** Filter daily expenses by IST business date on payload. */
export function buildStoreExpenseBusinessDateFilter(
  storeId: string,
  range: ResolvedDateRange,
): Record<string, unknown> {
  return {
    storeId,
    'payload.businessDate': { $gte: range.fromYmd, $lte: range.toYmd },
  };
}

export function buildStoreExpenseBusinessDateFilterForYmd(
  storeId: string,
  fromYmd: string,
  toYmd: string,
): Record<string, unknown> {
  return {
    storeId,
    'payload.businessDate': { $gte: fromYmd, $lte: toYmd },
  };
}

export type DailyExpenseTotals = { total: number; count: number };

export function sumDailyExpenses(
  docs: ReadonlyArray<{ payload?: Record<string, unknown> }>,
): DailyExpenseTotals {
  let total = 0;
  let count = 0;
  for (const doc of docs) {
    const payload = (doc.payload ?? {}) as Record<string, unknown>;
    const status = readString(payload.status) ?? 'posted';
    if (status === 'void' || status === 'cancelled') continue;
    const amount = readNumber(payload.amount);
    if (amount <= 0) continue;
    total += amount;
    count += 1;
  }
  return { total: roundMoney(total), count };
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

export function parseInvoiceSchemeDiscount(payload: Record<string, unknown>): number {
  const legacy = readNumber(payload.schemeDiscount);
  if (legacy > 0) return legacy;
  return roundMoney(readNumber(payload.schemeLineDiscount) + readNumber(payload.schemeBillDiscount));
}

export function parseInvoiceDiscounts(payload: Record<string, unknown>): number {
  return (
    readNumber(payload.itemDiscount) +
    readNumber(payload.cashDiscAmount) +
    parseInvoiceSchemeDiscount(payload)
  );
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

export function sumPaymentTotals(totals: PaymentTotals): number {
  return roundMoney(totals.cash + totals.card + totals.upi + totals.creditNote);
}

/** Inclusive bill total before checkout credit note is applied. */
export function parseInvoicePayableBeforeCredit(payload: Record<string, unknown>): number {
  const before = readNumber(payload.payableBeforeCredit);
  if (before > 0) return before;
  const payable = parseInvoiceNet(payload);
  const credit = parseInvoiceCreditApplied(payload);
  if (credit > 0 || payable > 0) return roundMoney(payable + credit);
  return 0;
}

/**
 * Bill amount for reconciliation reports: payable-before-credit when known,
 * else payable, else sum of recorded payments (sparse / inconsistent payloads).
 */
export function parseInvoiceBillAmount(
  payload: Record<string, unknown>,
  payments?: PaymentTotals,
): number {
  const pay = payments ?? parsePaymentTotals(payload);
  const paymentSum = sumPaymentTotals(pay);
  const beforeCredit = parseInvoicePayableBeforeCredit(payload);
  const payable = parseInvoiceNet(payload);

  if (beforeCredit > 0) return beforeCredit;
  if (paymentSum > 0 && payable <= 0) return paymentSum;
  if (payable > 0) return payable;
  return paymentSum;
}

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

function resolveSparsePaymentMode(payload: Record<string, unknown>): PaymentModeBucket {
  const mode = classifyPaymentProvider(readString(payload.paymentMode) ?? '');
  return mode === 'Other' ? 'Cash' : mode;
}

function addMissingCreditApplied(
  totals: PaymentTotals,
  creditApplied: number,
): void {
  if (creditApplied > totals.creditNote) {
    addPaymentToTotals(totals, 'Credit', roundMoney(creditApplied - totals.creditNote));
  }
}

export function parsePaymentTotals(payload: Record<string, unknown>): PaymentTotals {
  const totals: PaymentTotals = { cash: 0, card: 0, upi: 0, creditNote: 0 };

  if (isOnlineCodPending(payload)) {
    return totals;
  }

  const parsed = parseInvoicePayments(payload);
  for (const pay of parsed) {
    addPaymentToTotals(totals, pay.mode as PaymentModeBucket, pay.amount);
  }

  const payable = parseInvoiceNet(payload);
  const creditApplied = parseInvoiceCreditApplied(payload);
  const beforeCredit = parseInvoicePayableBeforeCredit(payload);
  const targetTotal = beforeCredit > 0 ? beforeCredit : payable;

  if (parsed.length === 0) {
    if (isOnlineCodBill(payload) && readOnlineCodStatus(payload).toLowerCase() === 'received') {
      const amount = parseOnlineCodAmount(payload);
      const mode = classifyPaymentProvider(
        parseOnlineCodReceivedPaymentMode(payload) ?? readString(payload.paymentMode) ?? '',
      );
      if (amount > 0 && mode !== 'Other') {
        addPaymentToTotals(totals, mode, amount);
      }
    } else if (targetTotal > 0) {
      addMissingCreditApplied(totals, creditApplied);
      const remaining = roundMoney(targetTotal - sumPaymentTotals(totals));
      if (remaining > 0) {
        addPaymentToTotals(totals, resolveSparsePaymentMode(payload), remaining);
      }
    }
  } else {
    addMissingCreditApplied(totals, creditApplied);
    const gap = roundMoney(targetTotal - sumPaymentTotals(totals));
    if (gap > 0.01) {
      addPaymentToTotals(totals, resolveSparsePaymentMode(payload), gap);
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

  const cashRefunded = readNumber(payload.cashRefunded);
  if (cashRefunded > 0) return cashRefunded;

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

export function parseReturnLines(payload: Record<string, unknown>): LineAgg[] {
  const returnLines = payload.returnLines ?? payload.lines;
  if (!Array.isArray(returnLines)) return [];
  const result: LineAgg[] = [];
  for (const line of returnLines) {
    if (!line || typeof line !== 'object') continue;
    const row = line as Record<string, unknown>;
    const qty = readNumber(row.returnQty) || readNumber(row.qty);
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
  return parseReturnLines(payload).reduce((s, l) => s + l.qty, 0);
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

/** Pre-discount line value: rate × qty, else amount, else net selling value. */
export function readLineGrossValue(row: Record<string, unknown>): number {
  const rate = readNumber(row.rate);
  const qty = readNumber(row.qty);
  if (rate > 0 && qty > 0) return rate * qty;

  const returnQty = readNumber(row.returnQty);
  if (rate > 0 && returnQty > 0) return rate * returnQty;

  const amount = readNumber(row.amount);
  if (amount > 0) return amount;

  return readLineSellingValue(row);
}

export function filterMarginLinesBySkuSet(
  lines: readonly LineMarginRow[],
  skuSet: ReadonlySet<string>,
): LineMarginRow[] {
  return lines.filter((l) => skuSet.has(l.sku));
}

export function sumVendorGrossFromLines(
  payload: Record<string, unknown>,
  skuSet: ReadonlySet<string>,
  options?: { returnLine?: boolean },
): number {
  const lines = options?.returnLine ? payload.returnLines ?? payload.lines : payload.lines;
  if (!Array.isArray(lines)) return 0;

  let sum = 0;
  for (const line of lines) {
    if (!line || typeof line !== 'object') continue;
    const row = line as Record<string, unknown>;
    const sku = readLineSku(row);
    if (!skuSet.has(sku)) continue;

    const qty = options?.returnLine
      ? readNumber(row.returnQty) || readNumber(row.qty)
      : readNumber(row.qty);
    if (qty <= 0) continue;

    sum += readLineGrossValue(row);
  }
  return sum;
}

export function countVendorLinesInPayload(
  payload: Record<string, unknown>,
  skuSet: ReadonlySet<string>,
  options?: { returnLine?: boolean },
): number {
  const lines = options?.returnLine ? payload.returnLines ?? payload.lines : payload.lines;
  if (!Array.isArray(lines)) return 0;

  let count = 0;
  for (const line of lines) {
    if (!line || typeof line !== 'object') continue;
    const row = line as Record<string, unknown>;
    if (!skuSet.has(readLineSku(row))) continue;
    const qty = options?.returnLine
      ? readNumber(row.returnQty) || readNumber(row.qty)
      : readNumber(row.qty);
    if (qty > 0) count += 1;
  }
  return count;
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

export function isOnlineCodBill(payload: Record<string, unknown>): boolean {
  return readString(payload.salesChannel)?.toLowerCase() === 'online';
}

export function readOnlineCodStatus(payload: Record<string, unknown>): string {
  const oc = payload.onlineCod;
  if (!oc || typeof oc !== 'object') return '';
  return readString((oc as Record<string, unknown>).status) ?? '';
}

export function isOnlineCodPending(payload: Record<string, unknown>): boolean {
  return isOnlineCodBill(payload) && readOnlineCodStatus(payload).toLowerCase() === 'pending';
}

export function parseOnlineCodAmount(payload: Record<string, unknown>): number {
  const oc = payload.onlineCod;
  if (oc && typeof oc === 'object') {
    const amt = readNumber((oc as Record<string, unknown>).amount);
    if (amt > 0) return amt;
  }
  return parseInvoiceNet(payload);
}

export function parseOnlineCodTransactionNo(payload: Record<string, unknown>): string | undefined {
  const oc = payload.onlineCod;
  if (!oc || typeof oc !== 'object') return undefined;
  return readString((oc as Record<string, unknown>).transactionNo);
}

export function parseOnlineCodReceivedPaymentMode(payload: Record<string, unknown>): string | undefined {
  const oc = payload.onlineCod;
  if (!oc || typeof oc !== 'object') return undefined;
  return readString((oc as Record<string, unknown>).receivedPaymentMode);
}

export type BillListStatusKey = 'completed' | 'partially_returned' | 'returned' | 'cancelled';
export type BillListPaymentModeKey = 'cash' | 'card' | 'upi' | 'credit' | 'mixed';

/** Default bills list window: last 30 IST calendar days through today. */
export function resolveBillsListDateRange(from?: string, to?: string): ResolvedDateRange {
  const biz = businessCalendarParts(businessNow());
  const toYmd = to?.trim() || formatYmdFromParts(biz.y, biz.m, biz.day);
  let fromYmd = from?.trim();
  if (!fromYmd) {
    const anchor = new Date(Date.UTC(biz.y, biz.m, biz.day - 30));
    const parts = businessCalendarParts(anchor);
    fromYmd = formatYmdFromParts(parts.y, parts.m, parts.day);
  }
  const fromParts = parseYmdParts(fromYmd);
  const toParts = parseYmdParts(toYmd);
  if (!fromParts || !toParts) {
    throw new Error('Invalid from or to date');
  }
  const { from: fromUtc } = businessDayBoundsUtc(fromParts.y, fromParts.m, fromParts.day);
  const { to: toUtc } = businessDayBoundsUtc(toParts.y, toParts.m, toParts.day);
  const days = Math.floor((toUtc.getTime() - fromUtc.getTime()) / (24 * 60 * 60 * 1000)) + 1;
  return {
    from: fromUtc,
    to: toUtc,
    fromYmd,
    toYmd,
    label: 'Selected range',
    bucketByMonth: days > 31,
    timezone: BUSINESS_TZ_IANA,
  };
}

export function sumInvoiceLineQty(payload: Record<string, unknown>): number {
  return sumLineQty(payload.lines);
}

export function resolveBillPaymentLabel(totals: PaymentTotals): {
  label: string;
  key: BillListPaymentModeKey;
} {
  const buckets: BillListPaymentModeKey[] = [];
  if (totals.cash > 0) buckets.push('cash');
  if (totals.card > 0) buckets.push('card');
  if (totals.upi > 0) buckets.push('upi');
  if (totals.creditNote > 0) buckets.push('credit');
  if (buckets.length === 0) return { label: 'Cash', key: 'cash' };
  if (buckets.length > 1) return { label: 'Mixed', key: 'mixed' };
  const key = buckets[0]!;
  const label =
    key === 'cash' ? 'Cash' : key === 'card' ? 'Card' : key === 'upi' ? 'UPI' : 'Credit';
  return { label, key };
}

export function resolveBillStatusKey(
  invoicePayload: Record<string, unknown>,
  returnPayloads: ReadonlyArray<Record<string, unknown>>,
): BillListStatusKey {
  const status = (readString(invoicePayload.status) ?? 'posted').toLowerCase();
  if (status === 'void' || status === 'cancelled') return 'cancelled';
  if (returnPayloads.length === 0) return 'completed';

  const invoiceQtyBySku = new Map<string, number>();
  for (const line of parseInvoiceLines(invoicePayload)) {
    invoiceQtyBySku.set(line.sku, (invoiceQtyBySku.get(line.sku) ?? 0) + line.qty);
  }

  const returnedQtyBySku = new Map<string, number>();
  for (const rp of returnPayloads) {
    for (const line of parseReturnLines(rp)) {
      returnedQtyBySku.set(line.sku, (returnedQtyBySku.get(line.sku) ?? 0) + line.qty);
    }
  }

  if (invoiceQtyBySku.size === 0) {
    return returnPayloads.length > 0 ? 'returned' : 'completed';
  }

  let allFullyReturned = true;
  let anyReturned = false;
  for (const [sku, invQty] of invoiceQtyBySku) {
    const retQty = returnedQtyBySku.get(sku) ?? 0;
    if (retQty > 0) anyReturned = true;
    if (retQty < invQty) allFullyReturned = false;
  }

  if (!anyReturned) return 'completed';
  return allFullyReturned ? 'returned' : 'partially_returned';
}

export function formatBillStatusLabel(key: BillListStatusKey): string {
  switch (key) {
    case 'completed':
      return 'Completed';
    case 'partially_returned':
      return 'Partially Returned';
    case 'returned':
      return 'Returned';
    case 'cancelled':
      return 'Cancelled';
  }
}
