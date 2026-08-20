import assert from 'node:assert/strict';
import { BadRequestException } from '@nestjs/common';
import {
  ACTIVE_OUTBOUND_DISPATCH_STATUSES,
  OutboundDispatchesService,
  assertOutboundDispatchTransition,
  isActiveOutboundDispatchStatus,
  normalizeOutboundDispatchPayload,
  normalizeOutboundDispatchStatus,
} from './outbound-dispatches.service';

type Row = Record<string, any>;

class FakeQuery<T> implements PromiseLike<T> {
  constructor(private readonly value: T) {}
  lean() {
    return Promise.resolve(this.value);
  }
  then<TResult1 = T, TResult2 = never>(
    onfulfilled?: ((value: T) => TResult1 | PromiseLike<TResult1>) | null,
    onrejected?: ((reason: unknown) => TResult2 | PromiseLike<TResult2>) | null,
  ): Promise<TResult1 | TResult2> {
    return Promise.resolve(this.value).then(onfulfilled, onrejected);
  }
}

function matches(row: Row, filter: Row): boolean {
  return Object.entries(filter).every(([key, expected]) => {
    if (expected && typeof expected === 'object' && '$in' in expected) {
      return (expected.$in as unknown[]).includes(row[key]);
    }
    return row[key] === expected;
  });
}

class FakeModel {
  rows: Row[] = [];

  findOne(filter: Row) {
    return new FakeQuery(this.rows.find((row) => matches(row, filter)) ?? null);
  }

  exists(filter: Row) {
    return Promise.resolve(Boolean(this.rows.find((row) => matches(row, filter))));
  }

  async create(input: Row) {
    const row: Row = {
      ...input,
      appliedEventIds: [...(input.appliedEventIds ?? [])],
      audit: [...(input.audit ?? [])],
      auditHistory: [...(input.auditHistory ?? [])],
    };
    row.save = async () => row;
    this.rows.push(row);
    return row;
  }

  async updateOne(filter: Row, update: Row) {
    const row = this.rows.find((candidate) => matches(candidate, filter));
    if (row && update.$set) Object.assign(row, update.$set);
    return { matchedCount: row ? 1 : 0 };
  }
}

async function run() {
  assert.equal(normalizeOutboundDispatchStatus('handedover'), 'HandedOver');
  assert.equal(normalizeOutboundDispatchStatus(' Delivered '), 'Delivered');
  assert.deepEqual(ACTIVE_OUTBOUND_DISPATCH_STATUSES, ['Draft', 'Ready', 'HandedOver']);
  assert.equal(isActiveOutboundDispatchStatus('Delivered'), false);

  for (const [from, to] of [
    ['Draft', 'Ready'],
    ['Ready', 'HandedOver'],
    ['HandedOver', 'Delivered'],
    ['Delivered', 'Returned'],
    ['Draft', 'Cancelled'],
  ] as const) {
    assert.doesNotThrow(() => assertOutboundDispatchTransition(from, to));
  }

  for (const [from, to] of [
    ['Draft', 'Delivered'],
    ['Ready', 'Draft'],
    ['HandedOver', 'Cancelled'],
    ['Cancelled', 'Ready'],
    ['Returned', 'Delivered'],
  ] as const) {
    assert.throws(
      () => assertOutboundDispatchTransition(from, to),
      BadRequestException,
    );
  }

  const wpfPayload = {
    dispatchNo: 'DSP-1',
    batchNo: 'DSPB-1',
    billNo: 'B-1',
    businessDate: '2026-08-12',
    billSnapshot: {
      billNo: 'B-1',
      customer: { customerCode: 'C-1' },
      lines: [{ sku: 'SKU-1', qty: 1 }],
    },
    shipTo: { name: 'Customer', addressLine1: 'Street' },
    carrier: { type: 'courier', name: 'Carrier', trackingNo: 'TRACK-1' },
    packageCount: 2,
    fee: {
      payer: 'customer',
      amount: 100.12,
      paymentMode: 'Cash',
      paymentReference: 'PAY-1',
    },
    auditHistory: [{ action: 'created', atUtc: '2026-08-12T10:00:00Z' }],
    status: 'Draft',
    revision: 1,
  };
  const normalized = normalizeOutboundDispatchPayload(wpfPayload);
  assert.equal(normalized.carrierType, 'courier');
  assert.equal(normalized.carrierName, 'Carrier');
  assert.equal(normalized.trackingNo, 'TRACK-1');
  assert.equal(normalized.feePayer, 'customer');
  assert.equal(normalized.dispatchFee, 100.12);
  assert.equal(normalized.feePaymentMode, 'Cash');
  assert.equal(normalized.lines?.[0]?.sku, 'SKU-1');
  assert.equal(normalized.auditHistory?.[0]?.action, 'created');

  const dispatchModel = new FakeModel();
  const receiptModel = new FakeModel();
  const expenseModel = new FakeModel();
  const invoiceModel = new FakeModel();
  const service = new OutboundDispatchesService(
    dispatchModel as never,
    receiptModel as never,
    expenseModel as never,
    invoiceModel as never,
  );
  const meta = { eventId: 'create-1', storeId: 'STORE-1', deviceId: 'POS-1' };
  await service.applyCreated(meta, wpfPayload);
  await service.applyCreated(meta, wpfPayload);
  assert.equal(dispatchModel.rows.length, 1, 'create event must be idempotent');
  const dispatch = dispatchModel.rows[0]!;
  assert.equal(dispatch.carrierType, 'courier');
  assert.equal(dispatch.feePayer, 'customer');
  assert.equal(dispatch.lines[0].sku, 'SKU-1');

  const active = await service.getActiveByBill('STORE-1', 'B-1');
  assert.equal(active?.dispatchNo, 'DSP-1');

  const charge = {
    dispatchNo: 'DSP-1',
    chargeReceipt: {
      receiptNo: 'RCPT-1',
      kind: 'dispatch_charge',
      amount: 100.12,
      status: 'posted',
    },
  };
  const chargeMeta = { ...meta, eventId: 'charge-1' };
  await service.applyChargeReceived(chargeMeta, charge);
  await service.applyChargeReceived(chargeMeta, charge);
  assert.equal(receiptModel.rows.length, 1, 'charge event must be idempotent');
  assert.equal(dispatch.revision, 1, 'linked charge must not consume WPF dispatch revision');
  assert.equal(dispatch.chargeReceiptNo, 'RCPT-1');

  const readyMeta = { ...meta, eventId: 'ready-1' };
  const readyPayload = {
    ...wpfPayload,
    status: 'Ready',
    revision: 2,
    chargeReceipt: charge.chargeReceipt,
    auditHistory: [
      ...(wpfPayload.auditHistory ?? []),
      { action: 'status_ready', atUtc: '2026-08-12T10:05:00Z' },
    ],
  };
  await service.applyUpdated(readyMeta, readyPayload);
  await service.applyUpdated(readyMeta, readyPayload);
  assert.equal(dispatch.status, 'Ready');
  assert.equal(dispatch.active, true);
  assert.equal(dispatch.revision, 2);

  await expenseModel.create({
    storeId: 'STORE-1',
    expenseNo: 'EXP-1',
    appliedEventIds: [],
    payload: { expenseNo: 'EXP-1', status: 'posted', revision: 1 },
  });
  dispatch.expenseNo = 'EXP-1';
  dispatch.linkedExpense = { expenseNo: 'EXP-1', status: 'posted', amount: 100.12 };
  dispatch.expense = dispatch.linkedExpense;
  const cancelMeta = { ...meta, eventId: 'cancel-1' };
  await service.applyCancelled(cancelMeta, {
    dispatchNo: 'DSP-1',
    reason: 'Customer cancelled',
    chargeReceipt: { receiptNo: 'RCPT-1', status: 'void' },
    linkedExpense: { expenseNo: 'EXP-1', status: 'void' },
  });
  await service.applyCancelled(cancelMeta, { dispatchNo: 'DSP-1' });
  assert.equal(dispatch.status, 'Cancelled');
  assert.equal(dispatch.active, false);
  assert.equal(dispatch.chargeReceipt.status, 'void');
  assert.equal(dispatch.linkedExpense.status, 'void');
  assert.equal(receiptModel.rows[0]?.payload.status, 'void');
  assert.equal(expenseModel.rows[0]?.payload.status, 'void');
  assert.equal(await service.getActiveByBill('STORE-1', 'B-1'), null);
}

void run()
  .then(() => console.log('outbound dispatch self-test passed'))
  .catch((error) => {
    console.error(error);
    process.exitCode = 1;
  });
