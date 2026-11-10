/**
 * Bill adjustment should patch invoice payable/lines after AdjustmentBillCreated sync.
 */
import 'reflect-metadata';
import assert from 'node:assert/strict';
import { BadRequestException, ConflictException } from '@nestjs/common';
import { applyAdjustmentToBillPayload } from './adjustment-bill-payload.util';
import { StoreSalesSyncService } from './store-sales-sync.service';

type InvoiceDoc = {
  storeId: string;
  invoiceNo: string;
  payload: Record<string, unknown>;
};

type AdjustmentDoc = {
  storeId: string;
  adjustmentNo: string;
  sourceEventId: string;
  deviceId: string;
  payload: Record<string, unknown>;
};

class FakeInvoiceModel {
  doc: InvoiceDoc | null = null;

  findOne(filter: Record<string, unknown>) {
    const match =
      this.doc &&
      filter.storeId === this.doc.storeId &&
      filter.invoiceNo === this.doc.invoiceNo
        ? this.doc
        : null;
    return { lean: async () => match };
  }

  async updateOne(
    filter: Record<string, unknown>,
    update: Record<string, unknown>,
  ) {
    if (!this.doc) return;
    if (filter.storeId !== this.doc.storeId || filter.invoiceNo !== this.doc.invoiceNo) return;
    const set = update.$set as { payload?: Record<string, unknown> } | undefined;
    if (set?.payload) this.doc.payload = structuredClone(set.payload);
  }
}

class FakeAdjustmentModel {
  docs: AdjustmentDoc[] = [];

  findOne(filter: Record<string, unknown>) {
    const match = this.docs.find((doc) => {
      if (filter.sourceEventId === doc.sourceEventId) return true;
      return filter.storeId === doc.storeId && filter.adjustmentNo === doc.adjustmentNo;
    });
    return { lean: async () => match ?? null };
  }

  async create(input: AdjustmentDoc) {
    this.docs.push(structuredClone(input));
    return input;
  }
}

function serviceWith(
  invoiceModel: FakeInvoiceModel,
  adjustmentModel: FakeAdjustmentModel,
): StoreSalesSyncService {
  return new StoreSalesSyncService(
    invoiceModel as never,
    undefined as never,
    adjustmentModel as never,
    undefined as never,
    undefined as never,
    undefined as never,
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
  const invoicePayload = {
    billNo: '20260930-001-1-0422',
    payable: 788,
    subTotal: 750,
    cgstTotal: 18.75,
    sgstTotal: 18.75,
    igstTotal: 0,
    taxTotal: 37.5,
    isInterState: false,
    lines: [
      {
        lineNo: 1,
        sku: 'SKU-00031',
        qty: 1,
        rate: 750,
        amount: 750,
        taxPercent: 5,
        cgstAmount: 18.75,
        sgstAmount: 18.75,
        igstAmount: 0,
      },
    ],
  };

  const adjustmentPayload = {
    adjustmentNo: 'ADJ-20261001-001-1-0847',
    originalBillNo: '20260930-001-1-0422',
    isInterState: false,
    adjustedPayable: 105,
    lines: [
      {
        lineNo: 1,
        adjustedQty: 1,
        adjustedRate: 100,
        adjustedAmount: 100,
        taxPercent: 5,
      },
    ],
  };

  const merged = applyAdjustmentToBillPayload(invoicePayload, adjustmentPayload);
  const mergedLines = merged.lines as Record<string, unknown>[];
  assert.equal(merged.payable, 105);
  assert.equal(mergedLines[0]?.rate, 100);
  assert.equal(mergedLines[0]?.amount, 100);
  assert.equal(merged.lastAdjustmentNo, 'ADJ-20261001-001-1-0847');
  assert.equal(merged.hasAdjustment, true);

  const invoiceModel = new FakeInvoiceModel();
  invoiceModel.doc = {
    storeId: 'store-001',
    invoiceNo: '20260930-001-1-0422',
    payload: structuredClone(invoicePayload),
  };
  const adjustmentModel = new FakeAdjustmentModel();
  const service = serviceWith(invoiceModel, adjustmentModel);

  await service.applyAdjustmentCreated(
    { eventId: 'evt-adj-1', storeId: 'store-001', deviceId: 'dev-1' },
    adjustmentPayload,
  );

  const patched = invoiceModel.doc!;
  assert.equal(patched.payload.payable, 105);
  assert.equal((patched.payload.lines as Record<string, unknown>[])[0]?.rate, 100);
  assert.deepEqual(patched.payload.adjustmentAppliedEventIds, ['evt-adj-1']);
  assert.equal(adjustmentModel.docs.length, 1);

  await service.applyAdjustmentCreated(
    { eventId: 'evt-adj-1', storeId: 'store-001', deviceId: 'dev-1' },
    adjustmentPayload,
  );
  assert.equal(adjustmentModel.docs.length, 1);

  let conflict: unknown;
  try {
    await service.applyAdjustmentCreated(
      { eventId: 'evt-adj-2', storeId: 'store-001', deviceId: 'dev-1' },
      {
        ...adjustmentPayload,
        adjustmentNo: 'ADJ-20261001-001-1-0999',
      },
    );
  } catch (err) {
    conflict = err;
  }
  assert.ok(conflict instanceof ConflictException);

  invoiceModel.doc = null;
  let missing: unknown;
  try {
    await service.applyAdjustmentCreated(
      { eventId: 'evt-adj-3', storeId: 'store-001', deviceId: 'dev-1' },
      adjustmentPayload,
    );
  } catch (err) {
    missing = err;
  }
  assert.ok(missing instanceof BadRequestException);

  console.log('adjustment-bill.selftest.ts: ok');
}

run().catch((err) => {
  console.error(err);
  process.exit(1);
});
