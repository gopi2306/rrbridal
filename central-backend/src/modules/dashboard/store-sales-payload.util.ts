import type { ResolvedDateRange, StoreSalesPeriodPreset } from './store-sales-dashboard.types';

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
  return Number.isFinite(n) ? n : 0;
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

export function parseInvoiceGross(payload: Record<string, unknown>): number {
  const payableBeforeCredit = readNumber(payload.payableBeforeCredit);
  if (payableBeforeCredit > 0) return payableBeforeCredit;

  const payable = readNumber(payload.payable);
  const creditApplied = readNumber(payload.creditApplied);
  if (payable > 0 || creditApplied > 0) return payable + creditApplied;

  const subTotal = readNumber(payload.subTotal);
  const taxTotal = readNumber(payload.taxTotal);
  if (subTotal > 0) return subTotal + taxTotal;

  return payable;
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

export function normalizePaymentMode(provider: string): string {
  const p = provider.trim().toLowerCase();
  if (p.includes('upi')) return 'UPI';
  if (p.includes('card')) return 'Card';
  if (p.includes('cash')) return 'Cash';
  if (p.includes('credit')) return 'Credit';
  return provider.trim() || 'Other';
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
    const provider = readString(row.provider) ?? 'Other';
    result.push({ mode: normalizePaymentMode(provider), amount });
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
