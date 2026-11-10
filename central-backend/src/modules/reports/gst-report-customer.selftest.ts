/**
 * Focused checks for customer details on GST bill-wise rows (code, phone, GSTIN,
 * B2B/B2C) and the customer-wise GST summary.
 */
import assert from 'node:assert/strict';
import {
  aggregateSalesByCustomer,
  aggregateSalesByInvoice,
  GstDocumentContext,
  parseInvoiceGstLines,
  parseReturnGstLines,
  readSalesCustomerDetails,
  WALK_IN_CUSTOMER_KEY,
} from './gst-report.util';

function line(sku: string, amount: number, taxAmount: number) {
  return {
    sku,
    qty: 1,
    amount,
    taxAmount,
    sgstAmount: taxAmount / 2,
    cgstAmount: taxAmount / 2,
    taxPercent: 5,
  };
}

function context(
  documentNo: string,
  documentType: GstDocumentContext['documentType'],
  documentDate: string,
  payload: Record<string, unknown>,
): GstDocumentContext {
  return {
    documentKey: `store-001|${documentType}|${documentNo}`,
    documentType,
    documentNo,
    documentDate,
    storeId: 'store-001',
    ...readSalesCustomerDetails(payload),
  };
}

const details = readSalesCustomerDetails({
  customerCode: ' C-0001 ',
  customerName: 'Anu Traders',
  customerPhone: '98765 43210',
  customerGstin: ' 33abcde1234f1z5 ',
});
assert.deepEqual(details, {
  customerCode: 'C-0001',
  customerName: 'Anu Traders',
  customerPhone: '9876543210',
  customerGstin: '33ABCDE1234F1Z5',
});
assert.deepEqual(readSalesCustomerDetails({ customerName: '  ', customerGstin: '' }), {});

const b2bPayload = {
  customerCode: 'C-0001',
  customerName: 'Anu Traders',
  customerPhone: '9876543210',
  customerGstin: '33ABCDE1234F1Z5',
  lines: [line('SKU-1', 1000, 50), line('SKU-2', 2000, 100)],
};
const b2cPayload = {
  customerName: 'Priya',
  customerPhone: '9123456780',
  lines: [line('SKU-3', 500, 25)],
};
const walkInPayload = { lines: [line('SKU-4', 200, 10)] };
const secondB2bPayload = { ...b2bPayload, lines: [line('SKU-5', 400, 20)] };
const returnPayload = { returnLines: [{ ...line('SKU-1', 1000, 50), returnQty: 1 }] };

const contributions = [
  ...parseInvoiceGstLines(b2bPayload, context('INV-1', 'invoice', '2026-10-02', b2bPayload)),
  ...parseInvoiceGstLines(b2cPayload, context('INV-2', 'invoice', '2026-10-03', b2cPayload)),
  ...parseInvoiceGstLines(walkInPayload, context('INV-3', 'invoice', '2026-10-04', walkInPayload)),
  ...parseInvoiceGstLines(
    secondB2bPayload,
    context('INV-4', 'invoice', '2026-10-05', secondB2bPayload),
  ),
  // Return carries no GSTIN of its own; the service fills it from the original bill.
  ...parseReturnGstLines(
    returnPayload,
    context('RET-1', 'return', '2026-10-06', { ...returnPayload, ...details }),
  ),
];

const byInvoice = aggregateSalesByInvoice(contributions);
assert.equal(byInvoice.length, 5);

const inv1 = byInvoice.find((row) => row.documentNo === 'INV-1');
assert.ok(inv1);
assert.equal(inv1.customerCode, 'C-0001');
assert.equal(inv1.customerPhone, '9876543210');
assert.equal(inv1.customerGstin, '33ABCDE1234F1Z5');
assert.equal(inv1.supplyCategory, 'B2B');
assert.equal(inv1.lineCount, 2);
assert.equal(inv1.taxableAmount, 3000);
assert.equal(inv1.taxAmount, 150);

const inv2 = byInvoice.find((row) => row.documentNo === 'INV-2');
assert.ok(inv2);
assert.equal(inv2.customerGstin, undefined);
assert.equal(inv2.supplyCategory, 'B2C');

const ret1 = byInvoice.find((row) => row.documentNo === 'RET-1');
assert.ok(ret1);
assert.equal(ret1.supplyCategory, 'B2B');
assert.equal(ret1.taxableAmount, -1000);

const byCustomer = aggregateSalesByCustomer(byInvoice);
assert.equal(byCustomer.length, 3);

const b2bCustomer = byCustomer.find((row) => row.customerGstin === '33ABCDE1234F1Z5');
assert.ok(b2bCustomer);
assert.equal(b2bCustomer.supplyCategory, 'B2B');
assert.equal(b2bCustomer.invoiceCount, 2);
assert.equal(b2bCustomer.returnCount, 1);
assert.equal(b2bCustomer.taxableAmount, 2400);
assert.equal(b2bCustomer.taxAmount, 120);
assert.equal(b2bCustomer.firstDocumentDate, '2026-10-02');
assert.equal(b2bCustomer.lastDocumentDate, '2026-10-06');

const walkIn = byCustomer.find((row) => row.customerKey === WALK_IN_CUSTOMER_KEY);
assert.ok(walkIn);
assert.equal(walkIn.supplyCategory, 'B2C');
assert.equal(walkIn.invoiceCount, 1);

assert.equal(byCustomer[0]?.supplyCategory, 'B2B');

console.log('gst-report-customer.selftest: ok');
