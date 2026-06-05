import { Types } from 'mongoose';
import { roundMoney } from '../../common/money.util';
import { PurchaseOrderLine, PurchaseOrderSupplierSnapshot } from './schemas/purchase-order.schema';

export interface PoRefreshProductSnapshot {
  _id?: Types.ObjectId;
  itemName?: string;
  shortName?: string;
  upcEanCode?: string;
  costPrice?: number;
  sellingPrice?: number;
  mrp?: number;
  gstPercent?: number;
}

export interface PoRefreshHeaderTotals {
  itemDiscAmount: number;
  surchargeAmount: number;
  taxAmount: number;
  cgstAmount: number;
  sgstAmount: number;
  cashDiscPercent: number;
  cashDiscount: number;
  netAmount: number;
}

function num(value: unknown, fallback = 0): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback;
}

function clampQty(value: unknown): number {
  return Math.max(0, num(value, 0));
}

/** Apply product master fields and recalculate line amounts per PO refresh formulas. */
export function refreshPurchaseOrderLine(
  line: PurchaseOrderLine,
  product: PoRefreshProductSnapshot,
): PurchaseOrderLine {
  const recdQty = clampQty(line.recdQty);
  const freeQty = clampQty(line.freeQty);

  const cost =
    typeof product.costPrice === 'number' && Number.isFinite(product.costPrice)
      ? product.costPrice
      : num(line.cost, 0);

  const taxPct =
    typeof product.gstPercent === 'number' && Number.isFinite(product.gstPercent)
      ? product.gstPercent
      : num(line.taxPercent, 0);

  const cgstPct = taxPct / 2;
  const sgstPct = taxPct / 2;

  const base = roundMoney(recdQty * cost);
  const disAmt = roundMoney(base * (num(line.discountPercent, 0) / 100));
  const sChrgAmt = roundMoney(base * (num(line.surchargePercent, 0) / 100));
  const taxAmt = roundMoney(base * (taxPct / 100));
  const cgstAmount = roundMoney(base * (cgstPct / 100));
  const sgstAmount = roundMoney(base * (sgstPct / 100));
  const amount = roundMoney(base + sChrgAmt - disAmt);
  const gross = roundMoney(amount + taxAmt);
  const netCost = roundMoney(gross / Math.max(1, recdQty));
  const cashDisAmt = roundMoney(gross * (num(line.cashDiscPercent, 0) / 100));
  const netAmt = roundMoney(gross - cashDisAmt);

  const description =
    (typeof product.itemName === 'string' && product.itemName.trim()) ||
    (typeof product.shortName === 'string' && product.shortName.trim()) ||
    line.description;

  const refreshed: PurchaseOrderLine = {
    ...line,
    recdQty,
    freeQty,
    cost,
    taxPercent: taxPct,
    cgstPercent: cgstPct,
    sgstPercent: sgstPct,
    discountAmount: disAmt,
    surchargeAmount: sChrgAmt,
    taxAmount: taxAmt,
    cgstAmount,
    sgstAmount,
    amount,
    netCost,
    cashDiscAmount: cashDisAmt,
    netAmount: netAmt,
  };

  if (product._id) refreshed.productId = product._id;
  if (product.upcEanCode !== undefined) refreshed.barcode = product.upcEanCode;
  if (description !== undefined) refreshed.description = description;
  if (typeof product.sellingPrice === 'number' && Number.isFinite(product.sellingPrice)) {
    refreshed.selling = product.sellingPrice;
  }
  if (typeof product.mrp === 'number' && Number.isFinite(product.mrp)) {
    refreshed.mrp = product.mrp;
  }

  return refreshed;
}

export function rollupPurchaseOrderHeaderTotals(
  lines: PurchaseOrderLine[],
  poCashDiscPercent: number | undefined,
  supplier: PurchaseOrderSupplierSnapshot,
): PoRefreshHeaderTotals {
  let itemDiscAmount = 0;
  let surchargeAmount = 0;
  let taxAmount = 0;
  let cgstAmount = 0;
  let sgstAmount = 0;
  let linesNet = 0;

  for (const line of lines) {
    itemDiscAmount += num(line.discountAmount, 0);
    surchargeAmount += num(line.surchargeAmount, 0);
    taxAmount += num(line.taxAmount, 0);
    cgstAmount += num(line.cgstAmount, 0);
    sgstAmount += num(line.sgstAmount, 0);
    linesNet += num(line.netAmount, 0);
  }

  const cashDiscPercent =
    poCashDiscPercent !== undefined && Number.isFinite(poCashDiscPercent)
      ? poCashDiscPercent
      : num(supplier.cashDiscount, 0);

  const roundedLinesNet = roundMoney(linesNet);
  const cashDiscount = roundMoney(roundedLinesNet * (cashDiscPercent / 100));
  const netAmount = roundMoney(roundedLinesNet - cashDiscount);

  return {
    itemDiscAmount: roundMoney(itemDiscAmount),
    surchargeAmount: roundMoney(surchargeAmount),
    taxAmount: roundMoney(taxAmount),
    cgstAmount: roundMoney(cgstAmount),
    sgstAmount: roundMoney(sgstAmount),
    cashDiscPercent,
    cashDiscount,
    netAmount,
  };
}
