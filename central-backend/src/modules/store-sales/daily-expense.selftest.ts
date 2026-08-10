/**
 * Focused checks for expanded daily-expense validation, cash aggregation, and
 * create/update/void sync idempotency.
 */
import 'reflect-metadata';
import assert from 'node:assert/strict';
import { BadRequestException } from '@nestjs/common';
import {
  normalizeDailyExpensePayload,
  readDailyExpenseCashAmount,
} from './daily-expense-payload';
import { StoreSalesSyncService } from './store-sales-sync.service';
import {
  sumDailyExpenseCashOutflow,
  sumDailyExpenses,
} from '../dashboard/store-sales-payload.util';
import { buildDetailSections } from '../dashboard/store-day-close-report-sections';

type ExpenseDoc = {
  storeId: string;
  expenseNo: string;
  sourceEventId: string;
  deviceId: string;
  appliedEventIds: string[];
  payload: Record<string, unknown>;
};

class FakeExpenseModel {
  doc: ExpenseDoc | null = null;
  createCount = 0;

  findOne(filter: Record<string, unknown>) {
    const match =
      this.doc &&
      (filter.sourceEventId === this.doc.sourceEventId ||
        (filter.storeId === this.doc.storeId && filter.expenseNo === this.doc.expenseNo))
        ? this.doc
        : null;
    return { lean: async () => match };
  }

  async create(input: ExpenseDoc) {
    this.createCount += 1;
    this.doc = structuredClone(input);
    return this.doc;
  }

  async updateOne(_filter: Record<string, unknown>, update: Record<string, unknown>) {
    if (!this.doc) return;
    const set = update.$set as Record<string, unknown> | undefined;
    if (set?.payload) this.doc.payload = structuredClone(set.payload as Record<string, unknown>);
    if (typeof set?.deviceId === 'string') this.doc.deviceId = set.deviceId;
    const addToSet = update.$addToSet as { appliedEventIds?: string } | undefined;
    const eventId = addToSet?.appliedEventIds;
    if (eventId && !this.doc.appliedEventIds.includes(eventId)) {
      this.doc.appliedEventIds.push(eventId);
    }
  }
}

function serviceWith(model: FakeExpenseModel): StoreSalesSyncService {
  return new StoreSalesSyncService(
    undefined as never,
    undefined as never,
    undefined as never,
    model as never,
    undefined as never,
    undefined as never,
    undefined as never,
    undefined as never,
    undefined as never,
    undefined as never,
    undefined as never,
    undefined as never,
  );
}

async function run() {
  const legacy = normalizeDailyExpensePayload({
    expenseNo: 'EXP-1',
    description: 'Tea',
    businessDate: '2026-08-04',
    amount: 100,
  });
  assert.equal(legacy.status, 'posted');
  assert.equal(readDailyExpenseCashAmount(legacy), 100);

  const split = normalizeDailyExpensePayload({
    expenseNo: 'EXP-2',
    description: 'Repairs',
    businessDate: '2026-08-04',
    amount: 1180,
    grandTotal: 1180,
    supplierName: 'Vendor',
    supplierGstin: '33ABCDE1234F1Z5',
    supplierStateCode: '33',
    storeStateCode: '33',
    supplierInvoiceNo: 'INV-2',
    supplierInvoiceDate: '2026-08-04',
    enteredAmount: 1180,
    gstMode: 'inclusive',
    supplyType: 'intra_state',
    taxableValue: 1000,
    gstRate: 18,
    cgst: 90,
    sgst: 90,
    totalTax: 180,
    payments: [
      { mode: 'Cash', amount: 180 },
      { mode: 'Bank Transfer', amount: 1000, reference: 'UTR-1' },
    ],
  });
  assert.equal(readDailyExpenseCashAmount(split), 180);
  assert.equal(readDailyExpenseCashAmount({ ...split, status: 'void' }), 0);
  const optionalRegistration = normalizeDailyExpensePayload({
    expenseNo: 'EXP-OPTIONAL',
    description: 'Local repair',
    businessDate: '2026-08-04',
    supplierName: 'Local Vendor',
    enteredAmount: 1180,
    amount: 1180,
    gstMode: 'inclusive',
    gstRate: 18,
    supplyType: 'intra_state',
    taxableAmount: 1000,
    cgstAmount: 90,
    sgstAmount: 90,
    payments: [{ mode: 'Cash', amount: 1180 }],
  });
  assert.equal(optionalRegistration.supplierGstin, undefined);
  assert.equal(optionalRegistration.supplierInvoiceNo, undefined);
  assert.deepEqual(
    sumDailyExpenses([
      { payload: legacy },
      { payload: split },
      { payload: { ...split, status: 'void' } },
    ]),
    { total: 1280, count: 2 },
  );
  assert.equal(sumDailyExpenseCashOutflow([{ payload: legacy }, { payload: split }]), 280);
  assert.throws(
    () => normalizeDailyExpensePayload({ ...split, payments: [{ provider: 'Cash', amount: 1 }] }),
    BadRequestException,
  );
  assert.throws(
    () => normalizeDailyExpensePayload({ ...split, supplierStateCode: '29' }),
    BadRequestException,
  );
  const expenseSection = buildDetailSections({
    counterRollup: [],
    bills: [],
    returns: [],
    adjustments: [],
    expenses: [{ expenseNo: 'EXP-2', supplier: 'Vendor', cashOutflow: '180.00' }],
    cashMovements: [],
    creditNoteCashouts: [],
    denominations: [],
  } as never).find((section) => section.name === 'EXPENSES');
  assert.ok(expenseSection?.headers.includes('Supplier GSTIN'));
  assert.ok(expenseSection?.headers.includes('Cash outflow'));

  const model = new FakeExpenseModel();
  const service = serviceWith(model);
  const createMeta = { eventId: 'create-1', storeId: 'store-1', deviceId: 'pos-1' };
  await service.applyDailyExpenseCreated(createMeta, split);
  await service.applyDailyExpenseCreated(createMeta, split);
  assert.equal(model.createCount, 1);

  const updateMeta = { eventId: 'update-1', storeId: 'store-1', deviceId: 'pos-1' };
  await service.applyDailyExpenseUpdated(updateMeta, {
    expenseNo: 'EXP-2',
    description: 'Emergency repairs',
    revision: 2,
  });
  await service.applyDailyExpenseUpdated(updateMeta, {
    expenseNo: 'EXP-2',
    description: 'Should not apply twice',
    revision: 2,
  });
  assert.equal(model.doc?.payload.description, 'Emergency repairs');
  assert.equal(model.doc?.appliedEventIds.filter((id) => id === 'update-1').length, 1);

  const voidMeta = { eventId: 'void-1', storeId: 'store-1', deviceId: 'pos-1' };
  await service.applyDailyExpenseVoided(voidMeta, {
    expenseNo: 'EXP-2',
    voidReason: 'Duplicate voucher',
    revision: 3,
  });
  await service.applyDailyExpenseVoided(voidMeta, {
    expenseNo: 'EXP-2',
    voidReason: 'Duplicate voucher',
    revision: 3,
  });
  assert.equal(model.doc?.payload.status, 'void');
  assert.equal(readDailyExpenseCashAmount(model.doc?.payload ?? {}), 0);
  assert.equal(model.doc?.appliedEventIds.filter((id) => id === 'void-1').length, 1);

  console.log('daily-expense.selftest: ok');
}

void run();
