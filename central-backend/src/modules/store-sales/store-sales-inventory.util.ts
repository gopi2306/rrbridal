import { readNumber, readString } from '../dashboard/store-sales-payload.util';

export type StockLine = { sku: string; qty: number };

export const STORE_INVOICE_POSTED = 'StoreInvoicePosted';
export const STORE_INVOICE_DELETED = 'StoreInvoiceDeleted';
export const STORE_SALE_RETURN_POSTED = 'StoreSaleReturnPosted';
export const STORE_SALE_EXCHANGE_POSTED = 'StoreSaleExchangePosted';

function readSku(line: Record<string, unknown>): string | undefined {
  return readString(line.sku) ?? readString(line.productCode);
}

function readQty(line: Record<string, unknown>, ...keys: string[]): number {
  for (const key of keys) {
    const n = readNumber(line[key]);
    if (n > 0) return n;
  }
  return 0;
}

function linesArray(payload: Record<string, unknown>, ...keys: string[]): unknown[] {
  for (const key of keys) {
    const raw = payload[key];
    if (Array.isArray(raw)) return raw;
  }
  return [];
}

/** SKUs where local stock was not decremented at post time. */
export function parseUndecrementedExceptionSkus(payload: Record<string, unknown>): Set<string> {
  const skip = new Set<string>();
  const raw = payload.stockExceptions;
  if (!Array.isArray(raw)) return skip;
  for (const item of raw) {
    if (!item || typeof item !== 'object') continue;
    const row = item as Record<string, unknown>;
    if (row.stockDecremented === false) {
      const sku = readString(row.sku);
      if (sku) skip.add(sku.toLowerCase());
    }
  }
  return skip;
}

export function aggregateBySku(lines: StockLine[]): StockLine[] {
  const bySku = new Map<string, number>();
  for (const line of lines) {
    const sku = line.sku.trim();
    if (!sku || line.qty <= 0) continue;
    bySku.set(sku, (bySku.get(sku) ?? 0) + line.qty);
  }
  return [...bySku.entries()].map(([sku, qty]) => ({ sku, qty }));
}

export function parseInvoiceStockLines(payload: Record<string, unknown>): StockLine[] {
  const skipSkus = parseUndecrementedExceptionSkus(payload);
  const rawLines = linesArray(payload, 'lines');
  const parsed: StockLine[] = [];

  for (const line of rawLines) {
    if (!line || typeof line !== 'object') continue;
    const row = line as Record<string, unknown>;
    const sku = readSku(row);
    if (!sku) continue;
    if (skipSkus.has(sku.toLowerCase())) continue;
    const qty = readQty(row, 'qty');
    if (qty <= 0) continue;
    parsed.push({ sku, qty });
  }

  return aggregateBySku(parsed);
}

export function parseReturnStockLines(payload: Record<string, unknown>): StockLine[] {
  const rawLines = linesArray(payload, 'returnLines', 'lines');
  const parsed: StockLine[] = [];

  for (const line of rawLines) {
    if (!line || typeof line !== 'object') continue;
    const row = line as Record<string, unknown>;
    const sku = readSku(row);
    if (!sku) continue;
    const qty = readQty(row, 'returnQty', 'qty');
    if (qty <= 0) continue;
    parsed.push({ sku, qty });
  }

  return aggregateBySku(parsed);
}

export function parseExchangeStockLines(payload: Record<string, unknown>): StockLine[] {
  const rawLines = linesArray(payload, 'exchangeLines');
  const parsed: StockLine[] = [];

  for (const line of rawLines) {
    if (!line || typeof line !== 'object') continue;
    const row = line as Record<string, unknown>;
    const sku = readSku(row);
    if (!sku) continue;
    const qty = readQty(row, 'qty');
    if (qty <= 0) continue;
    parsed.push({ sku, qty });
  }

  return aggregateBySku(parsed);
}
