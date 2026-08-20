import { roundMoney } from '../../common/money.util';
import {
  formatBusinessYmd,
  parseInvoiceBillAmount,
  parseOccurredAt,
  parsePaymentTotals,
  parseReturnLineQty,
  readNumber,
  readString,
  sumInvoiceLineQty,
} from '../dashboard/store-sales-payload.util';
import type {
  CustomerBillingBillDetail,
  CustomerBillingCustomerRow,
  CustomerBillingLineDetail,
  CustomerBillingPaymentSplits,
  CustomerBillingReturnDetail,
  CustomerBillingTotals,
} from './customer-billing-report.types';
import { parseReturnReportLines } from './sales-return-report.util';

export type CustomerBillingInvoiceInput = {
  invoiceNo: string;
  posCounter?: string;
  createdAt?: Date;
  payload?: Record<string, unknown>;
};

export type CustomerBillingReturnInput = {
  returnNo: string;
  kind?: string;
  createdAt?: Date;
  payload?: Record<string, unknown>;
};

const emptyPayments = (): CustomerBillingPaymentSplits => ({
  cash: 0,
  card: 0,
  upi: 0,
  creditNote: 0,
});

export function isPostedSalesPayload(payload: Record<string, unknown>): boolean {
  return (readString(payload.status) ?? 'posted').toLowerCase() === 'posted';
}

export function normalizeCustomerPhone(value: unknown): string {
  return (readString(value) ?? '').replace(/\D/g, '');
}

export function normalizeCustomerName(value: unknown): string {
  return (readString(value) ?? '').toLocaleLowerCase('en-IN').replace(/\s+/g, ' ').trim();
}

export function resolveCustomerIdentity(payload: Record<string, unknown>): {
  key: string;
  customerCode: string;
  customerName: string;
  customerPhone: string;
} {
  const customerCode = readString(payload.customerCode) ?? '';
  const customerName = readString(payload.customerName) ?? '';
  const customerPhone = readString(payload.customerPhone) ?? '';
  const normalizedCode = customerCode.toLocaleUpperCase('en-IN');
  const normalizedPhone = normalizeCustomerPhone(customerPhone);
  const normalizedName = normalizeCustomerName(customerName);

  const key = normalizedCode
    ? `code:${normalizedCode}`
    : normalizedPhone
      ? `phone:${normalizedPhone}`
      : normalizedName
        ? `name:${normalizedName}`
        : 'walk-in';

  return {
    key,
    customerCode,
    customerName: customerName || (key === 'walk-in' ? 'Walk-in Customer' : ''),
    customerPhone,
  };
}

function returnAmount(payload: Record<string, unknown>): number {
  const total = readNumber(payload.returnTotal);
  if (total > 0) return total;
  return roundMoney(
    parseReturnReportLines(payload).reduce((sum, line) => sum + line.returnAmount, 0),
  );
}

function mapReturn(doc: CustomerBillingReturnInput): CustomerBillingReturnDetail | null {
  const payload = (doc.payload ?? {}) as Record<string, unknown>;
  if (!isPostedSalesPayload(payload)) return null;
  const occurred = parseOccurredAt(payload, doc.createdAt);
  return {
    returnNo: readString(payload.returnNo) ?? doc.returnNo,
    returnDate:
      readString(payload.returnDate) ??
      readString(payload.businessDate) ??
      (occurred ? formatBusinessYmd(occurred) : ''),
    kind: doc.kind ?? readString(payload.kind) ?? 'return',
    returnMode: readString(payload.returnMode) ?? '',
    creditNoteNo: readString(payload.creditNoteNo) ?? '',
    qty: roundMoney(parseReturnLineQty(payload)),
    amount: returnAmount(payload),
  };
}

function lineAmount(row: Record<string, unknown>, qty: number): number {
  const amount = readNumber(row.amount);
  if (amount > 0) return amount;
  const revised = readNumber(row.revisedAmount);
  const revisedTax = readNumber(row.revisedTaxAmount);
  if (revised > 0) return roundMoney(revised + Math.max(0, revisedTax));
  const rate = readNumber(row.rate);
  if (rate > 0 && qty > 0) return roundMoney(rate * qty);
  return 0;
}

function mapBillLines(payload: Record<string, unknown>): CustomerBillingLineDetail[] {
  const lines = payload.lines;
  if (!Array.isArray(lines)) return [];

  const result: CustomerBillingLineDetail[] = [];
  let lineNo = 0;
  for (const line of lines) {
    if (!line || typeof line !== 'object') continue;
    const row = line as Record<string, unknown>;
    const qty = readNumber(row.qty);
    if (qty <= 0) continue;
    lineNo += 1;
    result.push({
      lineNo,
      sku: readString(row.sku) ?? readString(row.productCode) ?? 'UNKNOWN',
      description: readString(row.description) ?? readString(row.sku) ?? 'Product',
      hsn: readString(row.hsn) ?? '',
      qty: roundMoney(qty),
      rate: roundMoney(readNumber(row.rate)),
      amount: roundMoney(lineAmount(row, qty)),
      discountAmount: roundMoney(
        readNumber(row.discountAmount ?? row.itemDiscountAmount) +
          readNumber(row.cashDiscountAmount) +
          readNumber(row.schemeDiscountAmount),
      ),
      taxAmount: roundMoney(
        readNumber(row.taxAmount) ||
          readNumber(row.revisedTaxAmount) ||
          readNumber(row.cgstAmount) + readNumber(row.sgstAmount) + readNumber(row.igstAmount),
      ),
    });
  }
  return result;
}

function addPayments(
  target: CustomerBillingPaymentSplits,
  source: CustomerBillingPaymentSplits,
): void {
  target.cash = roundMoney(target.cash + source.cash);
  target.card = roundMoney(target.card + source.card);
  target.upi = roundMoney(target.upi + source.upi);
  target.creditNote = roundMoney(target.creditNote + source.creditNote);
}

function addBill(row: CustomerBillingCustomerRow, bill: CustomerBillingBillDetail): void {
  row.billCount += 1;
  row.qty = roundMoney(row.qty + bill.qty);
  row.grossAmount = roundMoney(row.grossAmount + bill.grossAmount);
  row.returnAmount = roundMoney(row.returnAmount + bill.returnAmount);
  row.netAmount = roundMoney(row.netAmount + bill.netAmount);
  addPayments(row.payments, bill.payments);
  row.bills.push(bill);
}

export function aggregateCustomerBilling(
  invoices: readonly CustomerBillingInvoiceInput[],
  returnsByBill: ReadonlyMap<string, readonly CustomerBillingReturnInput[]>,
): { data: CustomerBillingCustomerRow[]; totals: CustomerBillingTotals } {
  const rows = new Map<string, CustomerBillingCustomerRow>();

  for (const invoice of invoices) {
    const payload = (invoice.payload ?? {}) as Record<string, unknown>;
    if (!isPostedSalesPayload(payload)) continue;

    const identity = resolveCustomerIdentity(payload);
    const billNo = readString(payload.billNo) ?? invoice.invoiceNo;
    const occurred = parseOccurredAt(payload, invoice.createdAt);
    const returns = (returnsByBill.get(billNo) ?? [])
      .map(mapReturn)
      .filter((row): row is CustomerBillingReturnDetail => row !== null);
    const returned = roundMoney(returns.reduce((sum, row) => sum + row.amount, 0));
    const payments = parsePaymentTotals(payload);
    const grossAmount = roundMoney(parseInvoiceBillAmount(payload, payments));

    const bill: CustomerBillingBillDetail = {
      billNo,
      billDate:
        readString(payload.billDate) ??
        (occurred ? formatBusinessYmd(occurred) : ''),
      posCounter: readString(payload.posCounter) ?? invoice.posCounter ?? '',
      customerCode: identity.customerCode,
      customerName: identity.customerName,
      customerPhone: identity.customerPhone,
      qty: roundMoney(sumInvoiceLineQty(payload)),
      grossAmount,
      returnAmount: returned,
      netAmount: roundMoney(grossAmount - returned),
      payments: {
        cash: roundMoney(payments.cash),
        card: roundMoney(payments.card),
        upi: roundMoney(payments.upi),
        creditNote: roundMoney(payments.creditNote),
      },
      returns,
      lines: mapBillLines(payload),
    };

    let row = rows.get(identity.key);
    if (!row) {
      row = {
        customerKey: identity.key,
        customerCode: identity.customerCode,
        customerName: identity.customerName,
        customerPhone: identity.customerPhone,
        billCount: 0,
        qty: 0,
        grossAmount: 0,
        returnAmount: 0,
        netAmount: 0,
        payments: emptyPayments(),
        bills: [],
      };
      rows.set(identity.key, row);
    } else {
      if (!row.customerCode && identity.customerCode) row.customerCode = identity.customerCode;
      if (!row.customerName && identity.customerName) row.customerName = identity.customerName;
      if (!row.customerPhone && identity.customerPhone) row.customerPhone = identity.customerPhone;
    }
    addBill(row, bill);
  }

  const data = [...rows.values()];
  for (const row of data) {
    row.bills.sort((a, b) => b.billDate.localeCompare(a.billDate) || b.billNo.localeCompare(a.billNo));
  }
  data.sort(
    (a, b) =>
      b.netAmount - a.netAmount ||
      a.customerName.localeCompare(b.customerName) ||
      a.customerKey.localeCompare(b.customerKey),
  );

  const totals: CustomerBillingTotals = {
    customerCount: data.length,
    billCount: 0,
    qty: 0,
    grossAmount: 0,
    returnAmount: 0,
    netAmount: 0,
    payments: emptyPayments(),
  };
  for (const row of data) {
    totals.billCount += row.billCount;
    totals.qty = roundMoney(totals.qty + row.qty);
    totals.grossAmount = roundMoney(totals.grossAmount + row.grossAmount);
    totals.returnAmount = roundMoney(totals.returnAmount + row.returnAmount);
    totals.netAmount = roundMoney(totals.netAmount + row.netAmount);
    addPayments(totals.payments, row.payments);
  }

  return { data, totals };
}
