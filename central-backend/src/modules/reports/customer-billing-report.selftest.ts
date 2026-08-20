/**
 * Focused checks for customer identity fallback, posted billing, linked returns,
 * payment splits, totals, and the two-sheet XLSX export.
 */
import 'reflect-metadata';
import assert from 'node:assert/strict';
import * as XLSX from 'xlsx';
import { CustomerBillingReportExportService } from './customer-billing-report-export.service';
import { aggregateCustomerBilling, resolveCustomerIdentity } from './customer-billing-report.util';

function invoice(
  invoiceNo: string,
  customer: Record<string, unknown>,
  payable: number,
  payments: unknown[],
) {
  return {
    invoiceNo,
    posCounter: '1',
    createdAt: new Date('2026-08-01T05:00:00.000Z'),
    payload: {
      billNo: invoiceNo,
      status: 'posted',
      createdAtUtc: '2026-08-01T05:00:00.000Z',
      payable,
      lines: [{ sku: 'SKU-1', qty: 2 }],
      payments,
      ...customer,
    },
  };
}

async function run() {
  assert.equal(resolveCustomerIdentity({ customerCode: ' c001 ' }).key, 'code:C001');
  assert.equal(resolveCustomerIdentity({ customerPhone: '+91 98765-43210' }).key, 'phone:919876543210');
  assert.equal(resolveCustomerIdentity({ customerName: '  Anu   Devi ' }).key, 'name:anu devi');
  assert.equal(resolveCustomerIdentity({}).key, 'walk-in');

  const invoices = [
    invoice(
      'B-1',
      { customerCode: 'C001', customerName: 'Anu', customerPhone: '111' },
      100,
      [
        { provider: 'Cash', amount: 60 },
        { provider: 'PineLabs', amount: 40 },
      ],
    ),
    invoice('B-2', { customerCode: 'c001', customerName: 'Anu Devi' }, 50, [
      { provider: 'Razorpay', amount: 50 },
    ]),
    invoice('B-3', { customerPhone: '+91 98765 43210' }, 80, [{ provider: 'Cash', amount: 80 }]),
    invoice('B-4', { customerName: '  Meena   Kumari ' }, 70, [{ provider: 'Cash', amount: 70 }]),
    invoice('B-5', {}, 40, [{ provider: 'Cash', amount: 40 }]),
    {
      ...invoice('VOID-1', { customerCode: 'C001' }, 999, []),
      payload: { ...invoice('VOID-1', {}, 999, []).payload, status: 'void' },
    },
  ];
  const returnsByBill = new Map([
    [
      'B-1',
      [
        {
          returnNo: 'R-1',
          kind: 'return',
          createdAt: new Date('2026-08-10T05:00:00.000Z'),
          payload: {
            status: 'posted',
            originalBillNo: 'B-1',
            returnMode: 'cash_refund',
            returnTotal: 20,
            returnLines: [{ sku: 'SKU-1', returnQty: 1, lineTotal: 20 }],
          },
        },
        {
          returnNo: 'R-VOID',
          payload: { status: 'void', originalBillNo: 'B-1', returnTotal: 500 },
        },
      ],
    ],
  ]);

  const result = aggregateCustomerBilling(invoices, returnsByBill);
  assert.equal(result.data.length, 4);
  assert.equal(result.totals.billCount, 5);
  assert.equal(result.totals.qty, 10);
  assert.equal(result.totals.grossAmount, 340);
  assert.equal(result.totals.returnAmount, 20);
  assert.equal(result.totals.netAmount, 320);
  assert.deepEqual(result.totals.payments, { cash: 250, card: 40, upi: 50, creditNote: 0 });

  const codeCustomer = result.data.find((row) => row.customerKey === 'code:C001');
  assert.ok(codeCustomer);
  assert.equal(codeCustomer.billCount, 2);
  assert.equal(codeCustomer.returnAmount, 20);
  assert.equal(codeCustomer.netAmount, 130);
  assert.equal(
    codeCustomer.bills.find((bill) => bill.billNo === 'B-1')?.returns[0]?.returnNo,
    'R-1',
  );
  assert.equal(codeCustomer.bills.find((bill) => bill.billNo === 'B-1')?.lines[0]?.sku, 'SKU-1');
  assert.equal(codeCustomer.bills.find((bill) => bill.billNo === 'B-1')?.lines[0]?.qty, 2);

  const report = {
    period: {
      from: '2026-08-01',
      to: '2026-08-31',
      timezone: 'Asia/Kolkata',
      storeCode: 'store-1',
      storeName: 'Main Store',
    },
    filters: {},
    limit: 10000,
    truncated: false,
    total: 5,
    ...result,
  };
  const reportService = { buildReport: async () => report };
  const companyModel = {
    findOne: () => ({ lean: async () => ({ tradeName: 'RR Bridal', city: 'Chennai' }) }),
  };
  const exportService = new CustomerBillingReportExportService(
    reportService as never,
    companyModel as never,
  );
  const exported = await exportService.buildExport({
    from: '2026-08-01',
    to: '2026-08-31',
  });
  const workbook = XLSX.read(exported.buffer, { type: 'buffer' });
  assert.deepEqual(workbook.SheetNames, ['Summary', 'Bill Details', 'Line Items']);
  assert.match(exported.filename, /^customer-billing-store-1-2026-08-01-to-2026-08-31-\d{4}-\d{2}-\d{2}\.xlsx$/);
  const details = XLSX.utils.sheet_to_json<string[]>(workbook.Sheets['Bill Details']!, {
    header: 1,
  });
  assert.ok(details.flat().some((cell) => String(cell).includes('R-1 | 2026-08-10')));
  const lineItems = XLSX.utils.sheet_to_json<string[]>(workbook.Sheets['Line Items']!, {
    header: 1,
  });
  assert.ok(lineItems.flat().some((cell) => String(cell) === 'SKU-1'));
  assert.ok(lineItems.flat().some((cell) => String(cell) === 'B-1'));

  console.log('customer-billing-report.selftest: ok');
}

void run();
