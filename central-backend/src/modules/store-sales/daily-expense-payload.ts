import { BadRequestException } from '@nestjs/common';
import { roundMoney } from '../../common/money.util';

const SUPPORTED_PAYMENT_PROVIDERS = new Set([
  'cash',
  'card',
  'upi',
  'banktransfer',
]);

function requiredString(payload: Record<string, unknown>, key: string): string {
  const value = payload[key];
  if (typeof value !== 'string' || !value.trim()) {
    throw new BadRequestException(`${key} is required`);
  }
  return value.trim();
}

function optionalString(payload: Record<string, unknown>, key: string): string | undefined {
  const value = payload[key];
  if (value === undefined || value === null) return undefined;
  const result = String(value).trim();
  return result || undefined;
}

function requiredPositiveNumber(payload: Record<string, unknown>, key: string): number {
  const value = payload[key];
  const number = typeof value === 'number' ? value : Number(value);
  if (!Number.isFinite(number) || number <= 0) {
    throw new BadRequestException(`${key} must be positive`);
  }
  return roundMoney(number);
}

function validateOptionalNumber(
  payload: Record<string, unknown>,
  key: string,
  options?: { maximum?: number },
): void {
  if (payload[key] === undefined || payload[key] === null || payload[key] === '') return;
  const number = Number(payload[key]);
  if (!Number.isFinite(number) || number < 0) {
    throw new BadRequestException(`${key} must be a non-negative number`);
  }
  if (options?.maximum !== undefined && number > options.maximum) {
    throw new BadRequestException(`${key} must not exceed ${options.maximum}`);
  }
}

function optionalNumber(payload: Record<string, unknown>, key: string): number | undefined {
  const value = payload[key];
  if (value === undefined || value === null || value === '') return undefined;
  const number = Number(value);
  return Number.isFinite(number) ? roundMoney(number) : undefined;
}

function normalizeProvider(value: unknown): string {
  return String(value ?? '').trim().toLowerCase().replace(/[\s_-]+/g, '');
}

function validatePayments(payload: Record<string, unknown>, grandTotal: number): void {
  if (payload.payments === undefined || payload.payments === null) return;
  if (!Array.isArray(payload.payments) || payload.payments.length === 0) {
    throw new BadRequestException('payments must contain at least one payment leg');
  }

  let paymentTotal = 0;
  for (const payment of payload.payments) {
    if (!payment || typeof payment !== 'object') {
      throw new BadRequestException('each payment leg must be an object');
    }
    const row = payment as Record<string, unknown>;
    const provider = normalizeProvider(row.provider ?? row.Provider ?? row.mode);
    if (!SUPPORTED_PAYMENT_PROVIDERS.has(provider)) {
      throw new BadRequestException(
        'payment provider must be Cash, Card, UPI, or Bank Transfer',
      );
    }
    paymentTotal += requiredPositiveNumber(row, 'amount');
  }

  if (Math.abs(roundMoney(paymentTotal) - grandTotal) > 0.01) {
    throw new BadRequestException('payment leg total must equal grand total');
  }
}

/**
 * Validates and normalizes both legacy expenses and the expanded GST voucher payload.
 * Legacy callers may continue sending only expenseNo/description/businessDate/amount.
 */
export function normalizeDailyExpensePayload(
  payload: Record<string, unknown>,
): Record<string, unknown> {
  const expenseNo = requiredString(payload, 'expenseNo');
  const description = requiredString(payload, 'description');
  const businessDate = requiredString(payload, 'businessDate');
  if (!/^\d{4}-\d{2}-\d{2}$/.test(businessDate)) {
    throw new BadRequestException('businessDate must be YYYY-MM-DD');
  }

  const hasGrandTotal = payload.grandTotal !== undefined && payload.grandTotal !== null;
  const amount = hasGrandTotal
    ? requiredPositiveNumber(payload, 'grandTotal')
    : requiredPositiveNumber(payload, 'amount');
  if (
    hasGrandTotal &&
    payload.amount !== undefined &&
    Math.abs(requiredPositiveNumber(payload, 'amount') - amount) > 0.01
  ) {
    throw new BadRequestException('amount must equal grandTotal');
  }

  for (const key of [
    'taxableValue',
    'taxableAmount',
    'cgst',
    'cgstAmount',
    'sgst',
    'sgstAmount',
    'igst',
    'igstAmount',
    'totalTax',
  ]) {
    validateOptionalNumber(payload, key);
  }
  validateOptionalNumber(payload, 'gstRate', { maximum: 100 });

  const gstMode = (optionalString(payload, 'gstMode') ?? 'none').toLowerCase();
  if (!['none', 'inclusive', 'exclusive'].includes(gstMode)) {
    throw new BadRequestException('gstMode must be none, inclusive, or exclusive');
  }
  const gstRate = optionalNumber(payload, 'gstRate') ?? 0;
  const hasGst = gstMode !== 'none' && gstRate > 0;
  const gstin =
    optionalString(payload, 'supplierGstin') ??
    optionalString(payload, 'supplierGSTIN');
  if (gstin && !/^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][0-9A-Z]Z[0-9A-Z]$/i.test(gstin)) {
    throw new BadRequestException('supplierGstin is invalid');
  }
  const stateCode = optionalString(payload, 'supplierStateCode') ?? gstin?.slice(0, 2);
  if (stateCode && !/^\d{2}$/.test(stateCode)) {
    throw new BadRequestException('supplierStateCode must be two digits');
  }
  if (gstin && stateCode && gstin.slice(0, 2) !== stateCode) {
    throw new BadRequestException('supplierGstin state code must match supplierStateCode');
  }
  const storeStateCode = optionalString(payload, 'storeStateCode');
  if (storeStateCode && !/^\d{2}$/.test(storeStateCode)) {
    throw new BadRequestException('storeStateCode must be two digits');
  }
  const supplyType = (optionalString(payload, 'supplyType') ?? 'intra_state').toLowerCase();
  if (!['intra_state', 'inter_state'].includes(supplyType)) {
    throw new BadRequestException('supplyType must be intra_state or inter_state');
  }
  if (hasGst) {
    requiredString(payload, 'supplierName');
  }
  const invoiceDate = optionalString(payload, 'supplierInvoiceDate');
  if (invoiceDate && !/^\d{4}-\d{2}-\d{2}$/.test(invoiceDate)) {
    throw new BadRequestException('supplierInvoiceDate must be YYYY-MM-DD');
  }
  if (stateCode && storeStateCode) {
    const expected = stateCode === storeStateCode ? 'intra_state' : 'inter_state';
    if (supplyType !== expected) {
      throw new BadRequestException('supplyType does not match supplier and store GST state codes');
    }
  }

  const enteredAmount = optionalNumber(payload, 'enteredAmount');
  if (hasGst && enteredAmount !== undefined) {
    const expectedTaxable =
      gstMode === 'inclusive'
        ? roundMoney((enteredAmount * 100) / (100 + gstRate))
        : enteredAmount;
    const expectedTax = roundMoney(expectedTaxable * gstRate / 100);
    const expectedTotal =
      gstMode === 'inclusive' ? enteredAmount : roundMoney(expectedTaxable + expectedTax);
    if (Math.abs(expectedTotal - amount) > 0.01) {
      throw new BadRequestException('amount does not match enteredAmount and GST calculation');
    }
    const taxable = optionalNumber(payload, 'taxableAmount') ?? optionalNumber(payload, 'taxableValue');
    if (taxable !== undefined && Math.abs(taxable - expectedTaxable) > 0.01) {
      throw new BadRequestException('taxableAmount does not match the GST calculation');
    }
    const actualTax =
      (optionalNumber(payload, 'cgstAmount') ?? optionalNumber(payload, 'cgst') ?? 0) +
      (optionalNumber(payload, 'sgstAmount') ?? optionalNumber(payload, 'sgst') ?? 0) +
      (optionalNumber(payload, 'igstAmount') ?? optionalNumber(payload, 'igst') ?? 0);
    if (Math.abs(roundMoney(actualTax) - expectedTax) > 0.01) {
      throw new BadRequestException('GST component amounts do not match the GST calculation');
    }
  }

  validatePayments(payload, amount);

  return {
    ...payload,
    expenseNo,
    description,
    businessDate,
    amount,
    gstMode,
    gstRate,
    ...(stateCode ? { supplierStateCode: stateCode } : {}),
    ...(storeStateCode ? { storeStateCode } : {}),
    supplyType,
    ...(hasGrandTotal ? { grandTotal: amount } : {}),
    status: optionalString(payload, 'status') ?? 'posted',
  };
}

export function readDailyExpenseCashAmount(payload: Record<string, unknown>): number {
  const status = (optionalString(payload, 'status') ?? 'posted').toLowerCase();
  if (status !== 'posted') return 0;

  const total = Number(payload.grandTotal ?? payload.amount);
  const amount = Number.isFinite(total) && total > 0 ? roundMoney(total) : 0;
  if (!Array.isArray(payload.payments) || payload.payments.length === 0) {
    return amount;
  }

  let cash = 0;
  for (const payment of payload.payments) {
    if (!payment || typeof payment !== 'object') continue;
    const row = payment as Record<string, unknown>;
    if (normalizeProvider(row.provider ?? row.Provider ?? row.mode) !== 'cash') continue;
    const legAmount = Number(row.amount);
    if (Number.isFinite(legAmount) && legAmount > 0) cash += legAmount;
  }
  return roundMoney(cash);
}

export function formatDailyExpensePaymentSummary(payload: Record<string, unknown>): string {
  if (!Array.isArray(payload.payments) || payload.payments.length === 0) {
    const amount = Number(payload.grandTotal ?? payload.amount);
    return Number.isFinite(amount) && amount > 0 ? `Cash ${roundMoney(amount).toFixed(2)}` : '';
  }
  return payload.payments
    .filter((payment): payment is Record<string, unknown> => Boolean(payment && typeof payment === 'object'))
    .map((payment) => {
      const provider =
        optionalString(payment, 'provider') ??
        optionalString(payment, 'Provider') ??
        optionalString(payment, 'mode') ??
        '';
      const amount = Number(payment.amount);
      const reference = optionalString(payment, 'reference');
      return `${provider} ${Number.isFinite(amount) ? roundMoney(amount).toFixed(2) : '0.00'}${reference ? ` (${reference})` : ''}`;
    })
    .join('; ');
}
