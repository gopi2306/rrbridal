import { roundMoney } from '../../common/money.util';
import {
  formatBusinessYmd,
  parseOccurredAt,
  parsePaymentTotals,
  readNumber,
  readString,
} from '../dashboard/store-sales-payload.util';
import type {
  ItemDetailsReportFilters,
  ItemDetailsReportSummary,
  PurchaseGrnItemRow,
  PurchasePoItemRow,
  SalesItemRow,
  SohItemRow,
} from './item-details-report.types';

export type DateBounds = { from?: Date; to?: Date };

export type ProductRefInfo = {
  sku: string;
  itemName: string;
  brandName?: string;
  brandKey?: string;
  supplierKey?: string;
};

export function parseInclusiveDateBounds(from?: string, to?: string): DateBounds {
  const result: DateBounds = {};
  if (from) {
    const d = new Date(`${from}T00:00:00.000Z`);
    if (!Number.isNaN(d.getTime())) result.from = d;
  }
  if (to) {
    const d = new Date(`${to}T23:59:59.999Z`);
    if (!Number.isNaN(d.getTime())) result.to = d;
  }
  return result;
}

export function parseDocumentDate(value: unknown, fallback?: Date): Date | null {
  if (value instanceof Date && !Number.isNaN(value.getTime())) return value;
  if (typeof value === 'string' && value.trim()) {
    const trimmed = value.trim();
    const iso = new Date(trimmed);
    if (!Number.isNaN(iso.getTime())) return iso;
    const slash = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/.exec(trimmed);
    if (slash) {
      return new Date(Date.UTC(Number(slash[3]), Number(slash[2]) - 1, Number(slash[1])));
    }
  }
  return fallback ?? null;
}

export function isDateInBounds(date: Date | null, bounds: DateBounds): boolean {
  if (!date) return bounds.from === undefined && bounds.to === undefined;
  if (bounds.from && date.getTime() < bounds.from.getTime()) return false;
  if (bounds.to && date.getTime() > bounds.to.getTime()) return false;
  return true;
}

export function formatReportDate(date: Date | null): string {
  if (!date) return '';
  return formatBusinessYmd(date);
}

export function readPopulatedName(ref: unknown): string {
  if (!ref || typeof ref !== 'object') return '';
  const name = (ref as Record<string, unknown>).name;
  return typeof name === 'string' ? name.trim() : '';
}

export function readRefMatchKey(ref: unknown): string | undefined {
  if (!ref) return undefined;
  if (typeof ref === 'string') return ref.trim().toLowerCase();
  if (typeof ref !== 'object') return undefined;
  const row = ref as Record<string, unknown>;
  const code = readString(row.code);
  if (code) return code.toLowerCase();
  if (row._id != null) return String(row._id).toLowerCase();
  return undefined;
}

export function productInfoFromEnriched(product: Record<string, unknown>): ProductRefInfo {
  const sku = readString(product.sku) ?? '';
  const info: ProductRefInfo = {
    sku,
    itemName: readString(product.itemName) ?? sku,
  };
  const brandName = readPopulatedName(product.brandId);
  if (brandName) info.brandName = brandName;
  const brandKey = readRefMatchKey(product.brandId);
  if (brandKey) info.brandKey = brandKey;
  const supplierKey = readRefMatchKey(product.supplierNameId);
  if (supplierKey) info.supplierKey = supplierKey;
  return info;
}

export function passesProductFilters(
  sku: string,
  lineDescription: string,
  product: ProductRefInfo | undefined,
  filters: Pick<ItemDetailsReportFilters, 'sku' | 'search' | 'brandId' | 'supplierId'>,
): boolean {
  if (filters.sku && sku.toLowerCase() !== filters.sku.trim().toLowerCase()) return false;

  if (filters.search) {
    const term = filters.search.trim().toLowerCase();
    const name = (product?.itemName ?? lineDescription).toLowerCase();
    if (!sku.toLowerCase().includes(term) && !name.includes(term)) return false;
  }

  if (filters.brandId) {
    const key = filters.brandId.trim().toLowerCase();
    const brandKey = product?.brandKey?.toLowerCase();
    const brandName = product?.brandName?.toLowerCase();
    if (brandKey !== key && brandName !== key) return false;
  }

  if (filters.supplierId) {
    const key = filters.supplierId.trim().toLowerCase();
    if (product?.supplierKey?.toLowerCase() !== key) return false;
  }

  return true;
}

export function paginateRows<T>(rows: T[], limit: number, offset: number): { rows: T[]; truncated: boolean } {
  const slice = rows.slice(offset, offset + limit);
  return { rows: slice, truncated: offset + limit < rows.length };
}

export function buildPaymentSummary(payload: Record<string, unknown>): string {
  const totals = parsePaymentTotals(payload);
  const parts: string[] = [];
  if (totals.cash > 0) parts.push(`Cash ${roundMoney(totals.cash)}`);
  if (totals.card > 0) parts.push(`Card ${roundMoney(totals.card)}`);
  if (totals.upi > 0) parts.push(`UPI ${roundMoney(totals.upi)}`);
  if (totals.creditNote > 0) parts.push(`Credit ${roundMoney(totals.creditNote)}`);
  if (parts.length > 0) return parts.join(', ');
  return readString(payload.paymentMode) ?? '';
}

export function parseSalesLinesFromPayload(
  payload: Record<string, unknown>,
  ctx: {
    storeId: string;
    documentNo: string;
    invoiceNo?: string;
    docCreatedAt?: unknown;
    isReturn?: boolean;
  },
): SalesItemRow[] {
  const occurred = parseOccurredAt(payload, ctx.docCreatedAt);
  const billDate = formatReportDate(occurred);
  const salesman = readString(payload.salesman);
  const salesmanCode = readString(payload.salesmanCode);
  const paymentSummary = buildPaymentSummary(payload);
  const invoiceNo = ctx.invoiceNo ?? readString(payload.billNo) ?? ctx.documentNo;

  const rawLines = ctx.isReturn
    ? (Array.isArray(payload.returnLines)
        ? payload.returnLines
        : Array.isArray(payload.lines)
          ? payload.lines
          : [])
    : (Array.isArray(payload.lines) ? payload.lines : []);

  const result: SalesItemRow[] = [];
  for (const line of rawLines) {
    if (!line || typeof line !== 'object') continue;
    const lineRecord = line as Record<string, unknown>;
    const sku = readString(lineRecord.sku) ?? readString(lineRecord.productCode) ?? 'UNKNOWN';
    const description = readString(lineRecord.description) ?? sku;
    const qtyRaw = ctx.isReturn
      ? readNumber(lineRecord.returnQty) || readNumber(lineRecord.qty)
      : readNumber(lineRecord.qty);
    if (qtyRaw <= 0) continue;

    const qty = ctx.isReturn ? -Math.abs(qtyRaw) : qtyRaw;
    const rate = readNumber(lineRecord.rate);
    const amount =
      readNumber(lineRecord.amount)
      || readNumber(lineRecord.revisedAmount)
      || roundMoney(qty * rate);

    const saleRow: SalesItemRow = {
      storeId: ctx.storeId,
      invoiceNo,
      billDate,
      sku,
      productName: description,
      qty,
      rate,
      amount: ctx.isReturn ? -Math.abs(amount) : amount,
      paymentSummary,
      isReturn: Boolean(ctx.isReturn),
      documentNo: ctx.documentNo,
    };
    if (salesman) saleRow.salesman = salesman;
    if (salesmanCode) saleRow.salesmanCode = salesmanCode;
    result.push(saleRow);
  }

  return result;
}

export function aggregateSalesQtyBySku(salesRows: SalesItemRow[]): Map<string, number> {
  const map = new Map<string, number>();
  for (const row of salesRows) {
    const key = row.sku.trim().toLowerCase();
    if (!key) continue;
    map.set(key, roundMoney((map.get(key) ?? 0) + row.qty));
  }
  return map;
}

export function enrichSohRowsWithSalesQty(
  sohRows: SohItemRow[],
  salesQtyBySku: Map<string, number>,
): void {
  for (const row of sohRows) {
    const salesQty = salesQtyBySku.get(row.sku.trim().toLowerCase()) ?? 0;
    row.salesQty = salesQty;
    row.remainingQty = roundMoney(row.totalSoh - salesQty);
  }
}

export function buildItemDetailsSummary(
  poLines: PurchasePoItemRow[],
  grnLines: PurchaseGrnItemRow[],
  sohRows: SohItemRow[],
  salesRows: SalesItemRow[],
  truncated: ItemDetailsReportSummary['truncated'],
  totalCounts: {
    poLineCount: number;
    grnLineCount: number;
    sohSkuCount: number;
    salesLineCount: number;
  },
): ItemDetailsReportSummary {
  return {
    poLineCount: totalCounts.poLineCount,
    grnLineCount: totalCounts.grnLineCount,
    sohSkuCount: totalCounts.sohSkuCount,
    salesLineCount: totalCounts.salesLineCount,
    totalOrderedQty: roundMoney(poLines.reduce((s, r) => s + r.orderedQty, 0)),
    totalReceivedQty: roundMoney(grnLines.reduce((s, r) => s + r.receivedQty, 0)),
    totalSohQty: roundMoney(sohRows.reduce((s, r) => s + r.totalSoh, 0)),
    totalSoldQty: roundMoney(salesRows.reduce((s, r) => s + r.qty, 0)),
    totalSalesAmount: roundMoney(salesRows.reduce((s, r) => s + r.amount, 0)),
    truncated,
  };
}

export function normalizeReportFilters(query: {
  from?: string;
  to?: string;
  sku?: string;
  search?: string;
  storeId?: string;
  brandId?: string;
  supplierId?: string;
  limit?: number;
  offset?: number;
}): ItemDetailsReportFilters {
  return {
    ...(query.from ? { from: query.from } : {}),
    ...(query.to ? { to: query.to } : {}),
    ...(query.sku?.trim() ? { sku: query.sku.trim() } : {}),
    ...(query.search?.trim() ? { search: query.search.trim() } : {}),
    ...(query.storeId?.trim() ? { storeId: query.storeId.trim().toLowerCase() } : {}),
    ...(query.brandId?.trim() ? { brandId: query.brandId.trim() } : {}),
    ...(query.supplierId?.trim() ? { supplierId: query.supplierId.trim() } : {}),
    limit: query.limit ?? 1000,
    offset: query.offset ?? 0,
  };
}
