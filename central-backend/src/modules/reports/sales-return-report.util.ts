import { roundMoney } from '../../common/money.util';
import { readNumber, readString } from '../dashboard/store-sales-payload.util';
import { formatLegacyReportDate, readPopulatedName } from './purchase-return-report.util';

export { formatLegacyReportDate, readPopulatedName };

const BUSINESS_TZ_OFFSET_MS = 5.5 * 60 * 60 * 1000;

export function formatLegacyDateTime(value: unknown, fallback?: Date): string {
  const d = parseToDate(value, fallback);
  if (!d) return '';
  const shifted = new Date(d.getTime() + BUSINESS_TZ_OFFSET_MS);
  const day = String(shifted.getUTCDate()).padStart(2, '0');
  const month = String(shifted.getUTCMonth() + 1).padStart(2, '0');
  const year = shifted.getUTCFullYear();
  let hours = shifted.getUTCHours();
  const minutes = String(shifted.getUTCMinutes()).padStart(2, '0');
  const ampm = hours >= 12 ? 'PM' : 'AM';
  hours = hours % 12;
  if (hours === 0) hours = 12;
  const hourStr = String(hours).padStart(2, '0');
  return `${day}/${month}/${year} ${hourStr}:${minutes}${ampm}`;
}

function parseToDate(value: unknown, fallback?: Date): Date | null {
  if (value instanceof Date && !Number.isNaN(value.getTime())) return value;
  if (typeof value === 'string' && value.trim()) {
    const d = new Date(value.trim());
    if (!Number.isNaN(d.getTime())) return d;
  }
  if (fallback && !Number.isNaN(fallback.getTime())) return fallback;
  return null;
}

function readLineTax(row: Record<string, unknown>) {
  const cgst = readNumber(row.cgstAmount) || readNumber(row.cgstAmt);
  const sgst = readNumber(row.sgstAmount) || readNumber(row.sgstAmt);
  const igst = readNumber(row.igstAmount) || readNumber(row.igstAmt);
  let taxAmount = readNumber(row.revisedTaxAmount) || readNumber(row.taxAmount) || readNumber(row.taxAmt);
  if (taxAmount <= 0) taxAmount = roundMoney(cgst + sgst + igst);

  let taxPercent = readNumber(row.taxPercent);
  const cgstPct = readNumber(row.cgstPercent);
  const sgstPct = readNumber(row.sgstPercent);
  const igstPct = readNumber(row.igstPercent);
  if (taxPercent <= 0 && igstPct > 0) taxPercent = igstPct;
  if (taxPercent <= 0 && (cgstPct > 0 || sgstPct > 0)) taxPercent = roundMoney(cgstPct + sgstPct);

  return { taxAmount, taxPercent };
}

function readLineReturnAmount(row: Record<string, unknown>, taxAmount: number): number {
  const inclusive =
    readNumber(row.revisedInclusiveAmount) || readNumber(row.lineTotal) || readNumber(row.amount);
  if (inclusive > 0) return roundMoney(inclusive);

  const taxable = readNumber(row.revisedAmount) || readNumber(row.amount);
  if (taxable > 0) return roundMoney(taxable + taxAmount);

  const rate = readNumber(row.rate);
  const qty = readNumber(row.returnQty) || readNumber(row.qty);
  if (rate > 0 && qty > 0) return roundMoney(rate * qty);
  return 0;
}

function readLineSelling(row: Record<string, unknown>, qty: number, returnAmount: number): number {
  const rate = readNumber(row.rate);
  if (rate > 0) return roundMoney(rate);
  if (qty > 0 && returnAmount > 0) return roundMoney(returnAmount / qty);
  return 0;
}

export type ParsedReturnLine = {
  sku: string;
  itemName: string;
  qty: number;
  selling: number;
  mrp: number;
  taxPercent: number;
  taxAmount: number;
  returnAmount: number;
};

export function parseReturnReportLines(payload: Record<string, unknown>): ParsedReturnLine[] {
  const raw = payload.returnLines ?? payload.lines;
  if (!Array.isArray(raw)) return [];

  const result: ParsedReturnLine[] = [];
  for (const line of raw) {
    if (!line || typeof line !== 'object') continue;
    const row = line as Record<string, unknown>;
    const qty = readNumber(row.returnQty) || readNumber(row.qty);
    if (qty <= 0) continue;

    const sku = readString(row.sku) ?? readString(row.productCode) ?? '';
    const itemName = readString(row.description) ?? sku;
    const { taxAmount, taxPercent } = readLineTax(row);
    const returnAmount = readLineReturnAmount(row, taxAmount);
    const selling = readLineSelling(row, qty, returnAmount);
    const mrp = roundMoney(readNumber(row.mrp));

    result.push({
      sku,
      itemName,
      qty,
      selling,
      mrp,
      taxPercent,
      taxAmount,
      returnAmount,
    });
  }
  return result;
}

export function productCategoryFields(product: Record<string, unknown> | undefined) {
  if (!product) {
    return {
      department: '',
      category: '',
      subCategory: '',
      brand: '',
      weightAndSize: '',
      weightPerGmOrMl: '',
      offerGroup: '',
      statusCategory: '',
      colour: '',
    };
  }

  const statusRef = product.productStatusId as Record<string, unknown> | undefined;
  const statusName = readPopulatedName(statusRef);
  const statusCode =
    statusRef && typeof statusRef.code === 'string' ? statusRef.code.toUpperCase() : '';

  return {
    department: readPopulatedName(product.departmentId),
    category: readPopulatedName(product.categoryId),
    subCategory: readPopulatedName(product.subCategoryId),
    brand: readPopulatedName(product.brandId),
    weightAndSize: readPopulatedName(product.weightAndSizeId),
    weightPerGmOrMl: readPopulatedName(product.weightPerGmOrMlId),
    offerGroup: readPopulatedName(product.offerGroupId),
    statusCategory: statusName || statusCode,
    colour: readPopulatedName(product.colourId),
  };
}
