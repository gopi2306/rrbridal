/**
 * Bill summary reconciliation helpers.
 * Run: npx ts-node -r reflect-metadata scripts/test-bill-summary-payload.ts
 */
import assert from 'node:assert/strict';
import {
  parseInvoiceBillAmount,
  parseInvoiceDiscounts,
  parseInvoicePayableBeforeCredit,
  parsePaymentTotals,
  sumPaymentTotals,
} from '../src/modules/dashboard/store-sales-payload.util';

function testSparsePayableWithoutPayments() {
  const payload = { payable: 3599, paymentMode: 'Cash', payments: [] };
  const payments = parsePaymentTotals(payload);
  assert.equal(payments.cash, 3599);
  assert.equal(parseInvoiceBillAmount(payload, payments), 3599);
}

function testPayableZeroWithCashRecorded() {
  const payload = {
    payable: 0,
    payments: [{ provider: 'Cash', amount: 2659 }],
  };
  const payments = parsePaymentTotals(payload);
  assert.equal(sumPaymentTotals(payments), 2659);
  assert.equal(parseInvoiceBillAmount(payload, payments), 2659);
}

function testCreditNoteCheckout() {
  const payload = {
    payable: 0,
    creditApplied: 1500,
    payableBeforeCredit: 1500,
    paymentMode: 'CreditNote',
    payments: [{ provider: 'CreditNote', amount: 1500, reference: 'CN-1' }],
  };
  const payments = parsePaymentTotals(payload);
  assert.equal(payments.creditNote, 1500);
  assert.equal(parseInvoiceBillAmount(payload, payments), 1500);
}

function testPartialCreditWithCash() {
  const payload = {
    payable: 1000,
    creditApplied: 500,
    payableBeforeCredit: 1500,
    paymentMode: 'Cash',
    payments: [{ provider: 'Cash', amount: 1000 }],
  };
  const payments = parsePaymentTotals(payload);
  assert.equal(sumPaymentTotals(payments), 1500);
  assert.equal(parseInvoiceBillAmount(payload, payments), 1500);
}

function testSchemeDiscountFields() {
  const payload = {
    itemDiscount: 100,
    cashDiscAmount: 50,
    schemeLineDiscount: 200,
    schemeBillDiscount: 300,
  };
  assert.equal(parseInvoiceDiscounts(payload), 650);
}

function testOnlineCodPendingHasNoPayments() {
  const payload = {
    salesChannel: 'online',
    payable: 1200,
    onlineCod: { status: 'pending', amount: 1200 },
    payments: [],
  };
  const payments = parsePaymentTotals(payload);
  assert.equal(sumPaymentTotals(payments), 0);
  assert.equal(parseInvoicePayableBeforeCredit(payload), 1200);
}

testSparsePayableWithoutPayments();
testPayableZeroWithCashRecorded();
testCreditNoteCheckout();
testPartialCreditWithCash();
testSchemeDiscountFields();
testOnlineCodPendingHasNoPayments();

console.log('bill-summary payload tests: ok');
