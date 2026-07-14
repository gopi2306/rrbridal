import { roundMoney } from '../../common/money.util';
import { readNumber, readString } from './store-sales-payload.util';

export type BillMarginLine = {
  lineNo: number;
  sku: string;
  qty: number;
  costUnit: number;
  cost: number;
  selling: number;
  discount: number;
  originalTaxable: number;
  taxPercent: number;
  isIgst: boolean;
};

export type BillMarginTotals = {
  qty: number;
  cost: number;
  selling: number;
  discount: number;
};

/** Reverse-split GST-inclusive amount → taxable (ex GST). */
export function reverseSplitTaxable(inclusive: number, taxPercent: number): number {
  if (inclusive <= 0) return 0;
  if (taxPercent <= 0) return roundMoney(inclusive);
  const divisor = 1 + taxPercent / 100;
  return roundMoney(inclusive / divisor);
}

export function computeMarginPercentage(marginAmount: number, cost: number): number {
  if (!Number.isFinite(marginAmount) || !Number.isFinite(cost) || cost <= 0) return 0;
  return roundMoney((marginAmount / cost) * 100);
}

function detectIsIgst(row: Record<string, unknown>): boolean {
  return readNumber(row.igstPercent) > 0 || readNumber(row.igstAmount) > 0 || readNumber(row.igstAmt) > 0;
}

/** Parse invoice/bill lines into ex-GST margin components. */
export function parseBillExGstLines(
  lines: unknown,
  costBySku: ReadonlyMap<string, number>,
): BillMarginLine[] {
  if (!Array.isArray(lines)) return [];
  const result: BillMarginLine[] = [];
  for (const line of lines) {
    if (!line || typeof line !== 'object') continue;
    const row = line as Record<string, unknown>;
    const qty = readNumber(row.qty);
    if (qty <= 0) continue;

    const taxPercent = readNumber(row.taxPercent);
    const isIgst = detectIsIgst(row);
    const amount = readNumber(row.amount);
    const originalTaxable = reverseSplitTaxable(amount, taxPercent);

    let selling = readNumber(row.revisedAmount);
    if (selling <= 0) {
      const scheme = readNumber(row.schemeDiscountAmount);
      const itemDisc = readNumber(row.discountAmount);
      const cash = readNumber(row.cashDiscountAmount);
      const revisedInclusive = Math.max(0, amount - scheme - itemDisc - cash);
      selling = reverseSplitTaxable(revisedInclusive, taxPercent);
    }

    const discount = roundMoney(Math.max(0, originalTaxable - selling));
    const sku = readString(row.sku) ?? readString(row.productCode) ?? '';
    let costUnit = readNumber(row.costPrice);
    if (costUnit <= 0 && sku) costUnit = costBySku.get(sku) ?? 0;

    const lineNoRaw = row.lineNo;
    const lineNo =
      typeof lineNoRaw === 'number' && Number.isFinite(lineNoRaw)
        ? Math.trunc(lineNoRaw)
        : Number(lineNoRaw) || 0;

    result.push({
      lineNo,
      sku,
      qty,
      costUnit,
      cost: roundMoney(costUnit * qty),
      selling,
      discount,
      originalTaxable,
      taxPercent,
      isIgst,
    });
  }
  return result;
}

/** Apply adjustment payload lines: override qty/selling by lineNo then sku. */
export function applyAdjustmentOverrides(
  billLines: readonly BillMarginLine[],
  adjustmentLines: unknown,
): BillMarginLine[] {
  if (!Array.isArray(adjustmentLines) || adjustmentLines.length === 0) {
    return [...billLines];
  }

  const byLineNo = new Map<number, BillMarginLine>();
  const bySku = new Map<string, BillMarginLine>();
  for (const line of billLines) {
    if (line.lineNo > 0) byLineNo.set(line.lineNo, line);
    if (line.sku) bySku.set(line.sku.toLowerCase(), line);
  }

  const overriddenLineNos = new Set<number>();
  const overriddenSkus = new Set<string>();
  const result: BillMarginLine[] = [];

  for (const item of adjustmentLines) {
    if (!item || typeof item !== 'object') continue;
    const adj = item as Record<string, unknown>;
    const lineNoRaw = adj.lineNo;
    const lineNo =
      typeof lineNoRaw === 'number' && Number.isFinite(lineNoRaw)
        ? Math.trunc(lineNoRaw)
        : Number(lineNoRaw) || 0;
    const sku = readString(adj.sku) ?? '';

    let original: BillMarginLine | undefined;
    if (lineNo > 0) original = byLineNo.get(lineNo);
    if (!original && sku) original = bySku.get(sku.toLowerCase());
    if (!original) continue;

    const adjustedQty = readNumber(adj.adjustedQty);
    const adjustedAmount = readNumber(adj.adjustedAmount);
    // Adjustment payable math treats adjustedAmount as taxable (ex GST).
    const adjustedTaxable = adjustedAmount;
    const discount = roundMoney(Math.max(0, original.originalTaxable - adjustedTaxable));

    result.push({
      ...original,
      qty: adjustedQty,
      cost: roundMoney(original.costUnit * adjustedQty),
      selling: roundMoney(adjustedTaxable),
      discount,
    });

    if (original.lineNo > 0) overriddenLineNos.add(original.lineNo);
    if (original.sku) overriddenSkus.add(original.sku.toLowerCase());
  }

  for (const line of billLines) {
    if (line.lineNo > 0 && overriddenLineNos.has(line.lineNo)) continue;
    if (line.sku && overriddenSkus.has(line.sku.toLowerCase())) continue;
    result.push(line);
  }

  return result;
}

/** Aggregate return lines (ex GST); cost from original bill lines by lineNo/sku. */
export function aggregateReturnExGst(
  returnLines: unknown,
  originalBillLines: readonly BillMarginLine[],
): BillMarginTotals {
  if (!Array.isArray(returnLines)) return { qty: 0, cost: 0, selling: 0, discount: 0 };

  const byLineNo = new Map<number, BillMarginLine>();
  const bySku = new Map<string, BillMarginLine>();
  for (const line of originalBillLines) {
    if (line.lineNo > 0) byLineNo.set(line.lineNo, line);
    if (line.sku) bySku.set(line.sku.toLowerCase(), line);
  }

  let qty = 0;
  let cost = 0;
  let selling = 0;
  let discount = 0;

  for (const item of returnLines) {
    if (!item || typeof item !== 'object') continue;
    const row = item as Record<string, unknown>;
    let returnQty = readNumber(row.returnQty);
    if (returnQty <= 0) returnQty = readNumber(row.qty);
    if (returnQty <= 0) continue;

    const lineNoRaw = row.lineNo;
    const lineNo =
      typeof lineNoRaw === 'number' && Number.isFinite(lineNoRaw)
        ? Math.trunc(lineNoRaw)
        : Number(lineNoRaw) || 0;
    const sku = readString(row.sku) ?? readString(row.productCode) ?? '';

    let original: BillMarginLine | undefined;
    if (lineNo > 0) original = byLineNo.get(lineNo);
    if (!original && sku) original = bySku.get(sku.toLowerCase());

    const costUnit = original?.costUnit ?? readNumber(row.costPrice);
    cost = roundMoney(cost + costUnit * returnQty);
    qty = roundMoney(qty + returnQty);

    const taxPercent = readNumber(row.taxPercent) || original?.taxPercent || 0;

    // Return payload stores amount as taxable (ex GST).
    let taxable = readNumber(row.amount);
    if (taxable <= 0) taxable = readNumber(row.revisedAmount);
    if (taxable <= 0) {
      let inclusive = readNumber(row.revisedInclusiveAmount);
      if (inclusive <= 0) inclusive = readNumber(row.lineTotal);
      taxable = reverseSplitTaxable(inclusive, taxPercent);
    }
    selling = roundMoney(selling + taxable);

    const gross = readNumber(row.grossAmount);
    if (gross > 0) {
      const grossTaxable = reverseSplitTaxable(gross, taxPercent);
      discount = roundMoney(discount + Math.max(0, grossTaxable - taxable));
    } else {
      const discInclusive = readNumber(row.discountAmount) + readNumber(row.cashDiscountAmount);
      if (discInclusive > 0) {
        discount = roundMoney(discount + reverseSplitTaxable(discInclusive, taxPercent));
      }
    }
  }

  return { qty, cost, selling, discount };
}

export function sumLines(lines: readonly BillMarginLine[]): BillMarginTotals {
  let qty = 0;
  let cost = 0;
  let selling = 0;
  let discount = 0;
  for (const line of lines) {
    qty += line.qty;
    cost += line.cost;
    selling += line.selling;
    discount += line.discount;
  }
  return {
    qty: roundMoney(qty),
    cost: roundMoney(cost),
    selling: roundMoney(selling),
    discount: roundMoney(discount),
  };
}

export function netTotals(bill: BillMarginTotals, ret: BillMarginTotals): BillMarginTotals {
  return {
    qty: roundMoney(bill.qty - ret.qty),
    cost: roundMoney(bill.cost - ret.cost),
    selling: roundMoney(bill.selling - ret.selling),
    discount: roundMoney(bill.discount - ret.discount),
  };
}
