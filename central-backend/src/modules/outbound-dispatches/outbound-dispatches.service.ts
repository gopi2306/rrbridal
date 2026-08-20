import {
  BadRequestException,
  ConflictException,
  Injectable,
  NotFoundException,
} from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { roundMoney } from '../../common/money.util';
import { normalizeDailyExpensePayload } from '../store-sales/daily-expense-payload';
import {
  StoreDailyExpense,
  StoreDailyExpenseDocument,
} from '../store-sales/schemas/store-daily-expense.schema';
import {
  StorePaymentReceipt,
  StorePaymentReceiptDocument,
} from '../store-sales/schemas/store-payment-receipt.schema';
import {
  StoreInvoice,
  StoreInvoiceDocument,
} from '../store-sales/schemas/store-invoice.schema';
import {
  OUTBOUND_DISPATCH_STATUSES,
  OutboundDispatch,
  OutboundDispatchDocument,
  OutboundDispatchStatus,
} from './schemas/outbound-dispatch.schema';

export type OutboundDispatchEventMeta = {
  eventId: string;
  storeId: string;
  deviceId: string;
};

const TRANSITIONS: Record<OutboundDispatchStatus, readonly OutboundDispatchStatus[]> = {
  Draft: ['Ready', 'Cancelled'],
  Ready: ['HandedOver', 'Cancelled'],
  HandedOver: ['Delivered', 'Returned'],
  Delivered: ['Returned'],
  Cancelled: [],
  Returned: [],
};

export const ACTIVE_OUTBOUND_DISPATCH_STATUSES: readonly OutboundDispatchStatus[] = [
  'Draft',
  'Ready',
  'HandedOver',
];

export function isActiveOutboundDispatchStatus(status: OutboundDispatchStatus): boolean {
  return ACTIVE_OUTBOUND_DISPATCH_STATUSES.includes(status);
}

export function normalizeOutboundDispatchStatus(value: unknown): OutboundDispatchStatus {
  const text = String(value ?? '').trim().toLowerCase();
  const status = OUTBOUND_DISPATCH_STATUSES.find((candidate) => candidate.toLowerCase() === text);
  if (!status) {
    throw new BadRequestException(
      `status must be one of ${OUTBOUND_DISPATCH_STATUSES.join(', ')}`,
    );
  }
  return status;
}

export function assertOutboundDispatchTransition(
  from: OutboundDispatchStatus,
  to: OutboundDispatchStatus,
): void {
  if (from === to) return;
  if (!TRANSITIONS[from].includes(to)) {
    throw new BadRequestException(`Invalid outbound dispatch transition ${from} -> ${to}`);
  }
}

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
  return String(value).trim() || undefined;
}

function objectValue(payload: Record<string, unknown>, key: string): Record<string, unknown> {
  const value = payload[key];
  if (!value || typeof value !== 'object' || Array.isArray(value)) {
    throw new BadRequestException(`${key} must be an object`);
  }
  return value as Record<string, unknown>;
}

function optionalObject(
  payload: Record<string, unknown>,
  key: string,
): Record<string, unknown> | undefined {
  const value = payload[key];
  return value && typeof value === 'object' && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : undefined;
}

function positiveInteger(value: unknown, key: string): number {
  const number = Number(value);
  if (!Number.isInteger(number) || number < 1) {
    throw new BadRequestException(`${key} must be a positive integer`);
  }
  return number;
}

function nonNegativeMoney(value: unknown, key: string): number {
  const number = Number(value);
  if (!Number.isFinite(number) || number < 0) {
    throw new BadRequestException(`${key} must be a non-negative number`);
  }
  return roundMoney(number);
}

function isDuplicateKey(error: unknown): boolean {
  return Boolean(
    error &&
      typeof error === 'object' &&
      'code' in error &&
      (error as { code?: number }).code === 11000,
  );
}

export type NormalizedOutboundDispatchPayload = {
  billSnapshot?: Record<string, unknown>;
  lines?: Record<string, unknown>[];
  carrier?: Record<string, unknown>;
  carrierType?: string;
  carrierName?: string;
  trackingNo?: string;
  fee?: Record<string, unknown>;
  feePayer?: 'customer' | 'store';
  dispatchFee?: number;
  feePaymentMode?: string;
  feePaymentReference?: string;
  chargeReceipt?: Record<string, unknown>;
  linkedExpense?: Record<string, unknown>;
  auditHistory?: Record<string, unknown>[];
};

/** Accepts both WPF nested documents and the legacy flattened REST shape. */
export function normalizeOutboundDispatchPayload(
  payload: Record<string, unknown>,
): NormalizedOutboundDispatchPayload {
  const billSnapshot = optionalObject(payload, 'billSnapshot');
  const nestedLines = billSnapshot?.lines;
  const linesValue = payload.lines ?? nestedLines;
  if (linesValue !== undefined && !Array.isArray(linesValue)) {
    throw new BadRequestException('billSnapshot.lines/lines must be an array');
  }

  const nestedCarrier = optionalObject(payload, 'carrier');
  const carrierType =
    optionalString(nestedCarrier ?? {}, 'type') ?? optionalString(payload, 'carrierType');
  const carrierName =
    optionalString(nestedCarrier ?? {}, 'name') ?? optionalString(payload, 'carrierName');
  const trackingNo =
    optionalString(nestedCarrier ?? {}, 'trackingNo') ??
    optionalString(payload, 'trackingNo') ??
    optionalString(payload, 'trackingNumber');
  const carrier =
    nestedCarrier || carrierType || carrierName || trackingNo
      ? {
          ...(nestedCarrier ?? {}),
          ...(carrierType ? { type: carrierType.toLowerCase() } : {}),
          ...(carrierName ? { name: carrierName } : {}),
          ...(trackingNo ? { trackingNo } : {}),
        }
      : undefined;

  const nestedFee = optionalObject(payload, 'fee');
  const payerText =
    optionalString(nestedFee ?? {}, 'payer') ?? optionalString(payload, 'feePayer');
  let feePayer: 'customer' | 'store' | undefined;
  if (payerText) {
    const normalizedPayer = payerText.toLowerCase();
    if (normalizedPayer !== 'customer' && normalizedPayer !== 'store') {
      throw new BadRequestException('fee.payer/feePayer must be customer or store');
    }
    feePayer = normalizedPayer;
  }
  const amountValue = nestedFee?.amount ?? payload.dispatchFee;
  const dispatchFee =
    amountValue === undefined ? undefined : nonNegativeMoney(amountValue, 'fee.amount');
  const feePaymentMode =
    optionalString(nestedFee ?? {}, 'paymentMode') ??
    optionalString(payload, 'feePaymentMode');
  const feePaymentReference =
    optionalString(nestedFee ?? {}, 'paymentReference') ??
    optionalString(payload, 'feePaymentReference');
  const fee =
    nestedFee || feePayer || dispatchFee !== undefined || feePaymentMode || feePaymentReference
      ? {
          ...(nestedFee ?? {}),
          ...(feePayer ? { payer: feePayer } : {}),
          ...(dispatchFee !== undefined ? { amount: dispatchFee } : {}),
          ...(feePaymentMode ? { paymentMode: feePaymentMode } : {}),
          ...(feePaymentReference ? { paymentReference: feePaymentReference } : {}),
        }
      : undefined;

  const chargeReceipt =
    optionalObject(payload, 'chargeReceipt') ?? optionalObject(payload, 'receipt');
  const linkedExpense =
    optionalObject(payload, 'linkedExpense') ?? optionalObject(payload, 'expense');
  const auditValue = payload.auditHistory ?? payload.audit;
  if (auditValue !== undefined && !Array.isArray(auditValue)) {
    throw new BadRequestException('auditHistory/audit must be an array');
  }

  return {
    ...(billSnapshot ? { billSnapshot } : {}),
    ...(linesValue !== undefined
      ? { lines: linesValue as Record<string, unknown>[] }
      : {}),
    ...(carrier ? { carrier } : {}),
    ...(carrierType ? { carrierType: carrierType.toLowerCase() } : {}),
    ...(carrierName ? { carrierName } : {}),
    ...(trackingNo ? { trackingNo } : {}),
    ...(fee ? { fee } : {}),
    ...(feePayer ? { feePayer } : {}),
    ...(dispatchFee !== undefined ? { dispatchFee } : {}),
    ...(feePaymentMode ? { feePaymentMode } : {}),
    ...(feePaymentReference ? { feePaymentReference } : {}),
    ...(chargeReceipt ? { chargeReceipt } : {}),
    ...(linkedExpense ? { linkedExpense } : {}),
    ...(Array.isArray(auditValue)
      ? { auditHistory: auditValue as Record<string, unknown>[] }
      : {}),
  };
}

@Injectable()
export class OutboundDispatchesService {
  constructor(
    @InjectModel(OutboundDispatch.name)
    private readonly dispatchModel: Model<OutboundDispatchDocument>,
    @InjectModel(StorePaymentReceipt.name)
    private readonly paymentReceiptModel: Model<StorePaymentReceiptDocument>,
    @InjectModel(StoreDailyExpense.name)
    private readonly dailyExpenseModel: Model<StoreDailyExpenseDocument>,
    @InjectModel(StoreInvoice.name)
    private readonly invoiceModel: Model<StoreInvoiceDocument>,
  ) {}

  async applyCreated(
    meta: OutboundDispatchEventMeta,
    payload: Record<string, unknown>,
  ): Promise<void> {
    const normalized = normalizeOutboundDispatchPayload(payload);
    const alreadyCreated = await this.dispatchModel.findOne({
      createSourceEventId: meta.eventId,
    });
    if (alreadyCreated) {
      if (
        alreadyCreated.feePayer === 'store' &&
        normalized.linkedExpense
      ) {
        await this.persistLinkedExpense(
          meta,
          alreadyCreated.dispatchNo,
          alreadyCreated.billNo,
          normalized.linkedExpense,
        );
      }
      return;
    }

    const dispatchNo = requiredString(payload, 'dispatchNo');
    const billNo =
      optionalString(payload, 'billNo') ??
      optionalString(normalized.billSnapshot ?? {}, 'billNo');
    if (!billNo) throw new BadRequestException('billNo is required');
    const businessDate = requiredString(payload, 'businessDate');
    if (!/^\d{4}-\d{2}-\d{2}$/.test(businessDate)) {
      throw new BadRequestException('businessDate must be YYYY-MM-DD');
    }
    const status =
      payload.status === undefined ? 'Draft' : normalizeOutboundDispatchStatus(payload.status);
    if (status !== 'Draft') {
      throw new BadRequestException('A new outbound dispatch must start in Draft');
    }
    if (!normalized.billSnapshot) throw new BadRequestException('billSnapshot must be an object');
    if (!normalized.carrierType) throw new BadRequestException('carrier.type/carrierType is required');
    if (!normalized.feePayer) throw new BadRequestException('fee.payer/feePayer is required');
    const feePayer = normalized.feePayer;
    const now = new Date().toISOString();
    const audit = [...(normalized.auditHistory ?? [])];
    audit.push({
      action: 'Created',
      status: 'Draft',
      atUtc: optionalString(payload, 'createdAtUtc') ?? now,
      by: optionalString(payload, 'createdBy'),
      eventId: meta.eventId,
      deviceId: meta.deviceId,
    });

    let created: OutboundDispatchDocument;
    try {
      created = await this.dispatchModel.create({
        dispatchNo,
        batchNo: optionalString(payload, 'batchNo'),
        storeCode: meta.storeId,
        deviceId: meta.deviceId,
        posCounter:
          optionalString(payload, 'posCounter') ?? optionalString(payload, 'counter'),
        businessDate,
        billNo,
        billSnapshot: normalized.billSnapshot,
        lines: normalized.lines ?? [],
        shipTo: objectValue(payload, 'shipTo'),
        carrierType: normalized.carrierType,
        carrier: normalized.carrier ?? { type: normalized.carrierType },
        carrierName: normalized.carrierName,
        trackingNo: normalized.trackingNo,
        packageCount: positiveInteger(payload.packageCount ?? 1, 'packageCount'),
        feePayer,
        dispatchFee: normalized.dispatchFee ?? 0,
        fee: normalized.fee ?? { payer: feePayer, amount: normalized.dispatchFee ?? 0 },
        feePaymentMode: normalized.feePaymentMode,
        feePaymentReference: normalized.feePaymentReference,
        chargeReceiptNo:
          optionalString(payload, 'chargeReceiptNo') ??
          optionalString(normalized.chargeReceipt ?? {}, 'receiptNo'),
        expenseNo:
          optionalString(payload, 'expenseNo') ??
          optionalString(normalized.linkedExpense ?? {}, 'expenseNo'),
        chargeReceipt: normalized.chargeReceipt,
        expense: normalized.linkedExpense,
        linkedExpense: normalized.linkedExpense,
        status,
        active: true,
        audit,
        auditHistory: audit,
        revision: 1,
        appliedEventIds: [meta.eventId],
        createSourceEventId: meta.eventId,
        updatedAtUtc: now,
      });
    } catch (error: unknown) {
      if (isDuplicateKey(error)) {
        if (await this.dispatchModel.exists({ createSourceEventId: meta.eventId })) return;
        throw new ConflictException(
          `Dispatch '${dispatchNo}' or an active dispatch for bill '${billNo}' already exists`,
        );
      }
      throw error;
    }

    if (feePayer === 'store' && normalized.linkedExpense) {
      await this.persistLinkedExpense(meta, dispatchNo, billNo, normalized.linkedExpense);
    }
    await this.updateBillDispatchSnapshot(created);
  }

  async applyUpdated(
    meta: OutboundDispatchEventMeta,
    payload: Record<string, unknown>,
  ): Promise<void> {
    const dispatch = await this.requireDispatch(meta.storeId, requiredString(payload, 'dispatchNo'));
    if (dispatch.appliedEventIds.includes(meta.eventId)) return;
    const normalized = normalizeOutboundDispatchPayload(payload);
    const requestedStatus =
      payload.status === undefined
        ? dispatch.status
        : normalizeOutboundDispatchStatus(payload.status);
    if (requestedStatus !== dispatch.status) {
      assertOutboundDispatchTransition(dispatch.status, requestedStatus);
    } else if (dispatch.status !== 'Draft') {
      throw new BadRequestException(`Dispatch in ${dispatch.status} cannot be edited`);
    }
    const requestedRevision = Number(payload.revision ?? payload.expectedRevision);
    if (Number.isFinite(requestedRevision) && requestedRevision !== dispatch.revision + 1) {
      throw new ConflictException(
        `Dispatch revision conflict: current ${dispatch.revision}, received ${requestedRevision}`,
      );
    }

    const mutableStrings = ['batchNo', 'chargeReceiptNo', 'expenseNo', 'posCounter'] as const;
    for (const key of mutableStrings) {
      if (payload[key] !== undefined) {
        (dispatch as unknown as Record<string, unknown>)[key] = optionalString(payload, key);
      }
    }
    if (payload.shipTo !== undefined) dispatch.shipTo = objectValue(payload, 'shipTo');
    if (payload.billSnapshot !== undefined) {
      dispatch.billSnapshot = normalized.billSnapshot ?? objectValue(payload, 'billSnapshot');
    }
    if (normalized.lines !== undefined) {
      dispatch.lines = normalized.lines;
    }
    if (payload.packageCount !== undefined) {
      dispatch.packageCount = positiveInteger(payload.packageCount, 'packageCount');
    }
    if (normalized.carrier) {
      dispatch.carrier = normalized.carrier;
      if (normalized.carrierType) dispatch.carrierType = normalized.carrierType;
      if (normalized.carrierName !== undefined) dispatch.carrierName = normalized.carrierName;
      if (normalized.trackingNo !== undefined) dispatch.trackingNo = normalized.trackingNo;
    }
    if (normalized.fee) {
      dispatch.fee = normalized.fee;
      if (normalized.feePayer) dispatch.feePayer = normalized.feePayer;
      if (normalized.dispatchFee !== undefined) dispatch.dispatchFee = normalized.dispatchFee;
      if (normalized.feePaymentMode !== undefined) {
        dispatch.feePaymentMode = normalized.feePaymentMode;
      }
      if (normalized.feePaymentReference !== undefined) {
        dispatch.feePaymentReference = normalized.feePaymentReference;
      }
    }
    if (normalized.chargeReceipt) {
      dispatch.chargeReceipt = normalized.chargeReceipt;
      const receiptNo = optionalString(normalized.chargeReceipt, 'receiptNo');
      if (receiptNo) dispatch.chargeReceiptNo = receiptNo;
    }
    if (normalized.linkedExpense) {
      dispatch.expense = normalized.linkedExpense;
      dispatch.linkedExpense = normalized.linkedExpense;
    }
    if (dispatch.feePayer === 'store' && normalized.linkedExpense) {
      await this.persistLinkedExpense(
        meta,
        dispatch.dispatchNo,
        dispatch.billNo,
        normalized.linkedExpense,
      );
      const linkedExpenseNo = optionalString(normalized.linkedExpense, 'expenseNo');
      if (linkedExpenseNo) dispatch.expenseNo = linkedExpenseNo;
    }

    const previousStatus = dispatch.status;
    dispatch.status = requestedStatus;
    dispatch.active = isActiveOutboundDispatchStatus(requestedStatus);
    dispatch.revision =
      Number.isFinite(requestedRevision) ? requestedRevision : dispatch.revision + 1;
    dispatch.updatedAtUtc =
      optionalString(payload, 'updatedAtUtc') ?? new Date().toISOString();
    dispatch.appliedEventIds.push(meta.eventId);
    if (normalized.auditHistory) {
      dispatch.audit = [...normalized.auditHistory];
      dispatch.auditHistory = [...normalized.auditHistory];
    } else {
      const entry = {
        action: previousStatus === requestedStatus ? 'Updated' : 'StatusChanged',
        fromStatus: previousStatus,
        toStatus: requestedStatus,
        atUtc: dispatch.updatedAtUtc,
        by: optionalString(payload, 'updatedBy'),
        eventId: meta.eventId,
        deviceId: meta.deviceId,
        revision: dispatch.revision,
      };
      dispatch.audit.push(entry);
      dispatch.auditHistory.push(entry);
    }
    if (requestedStatus === 'Cancelled') {
      dispatch.cancelledAtUtc = dispatch.updatedAtUtc;
      await this.voidLinkedRecords(dispatch, meta, payload);
    }
    await dispatch.save();
    await this.updateBillDispatchSnapshot(dispatch);
  }

  async applyStatusChanged(
    meta: OutboundDispatchEventMeta,
    payload: Record<string, unknown>,
  ): Promise<void> {
    const dispatch = await this.requireDispatch(meta.storeId, requiredString(payload, 'dispatchNo'));
    if (dispatch.appliedEventIds.includes(meta.eventId)) return;
    const next = normalizeOutboundDispatchStatus(
      payload.status ?? payload.newStatus ?? payload.toStatus,
    );
    await this.changeStatus(dispatch, meta, payload, next);
  }

  async applyChargeReceived(
    meta: OutboundDispatchEventMeta,
    payload: Record<string, unknown>,
  ): Promise<void> {
    const dispatch = await this.requireDispatch(meta.storeId, requiredString(payload, 'dispatchNo'));
    if (dispatch.appliedEventIds.includes(meta.eventId)) return;
    if (dispatch.feePayer !== 'customer') {
      throw new BadRequestException('Charge receipt is only valid when feePayer is customer');
    }
    if (dispatch.status === 'Cancelled' || dispatch.status === 'Returned') {
      throw new BadRequestException(`Cannot receive charge for a ${dispatch.status} dispatch`);
    }
    const normalized = normalizeOutboundDispatchPayload(payload);
    const receipt = normalized.chargeReceipt ?? payload;
    const receiptNo =
      optionalString(receipt, 'receiptNo') ?? optionalString(payload, 'chargeReceiptNo');
    if (!receiptNo) throw new BadRequestException('receipt.receiptNo is required');
    const amount = nonNegativeMoney(receipt.amount ?? payload.amount ?? dispatch.dispatchFee, 'amount');
    if (amount <= 0) throw new BadRequestException('amount must be positive');

    try {
      await this.paymentReceiptModel.create({
        storeId: meta.storeId,
        receiptNo,
        billNo: dispatch.billNo,
        sourceEventId: meta.eventId,
        deviceId: meta.deviceId,
        payload: {
          ...receipt,
          receiptNo,
          amount,
          purpose: 'outbound_dispatch_charge',
          dispatchNo: dispatch.dispatchNo,
          billNo: dispatch.billNo,
        },
      });
    } catch (error: unknown) {
      if (!isDuplicateKey(error)) throw error;
      const sameEvent = await this.paymentReceiptModel.exists({ sourceEventId: meta.eventId });
      if (!sameEvent) throw new ConflictException(`Receipt '${receiptNo}' already exists`);
    }

    dispatch.chargeReceiptNo = receiptNo;
    dispatch.chargeReceipt = { ...receipt, receiptNo, amount };
    dispatch.updatedAtUtc = new Date().toISOString();
    dispatch.appliedEventIds.push(meta.eventId);
    const auditEntry = {
      action: 'ChargeReceived',
      amount,
      receiptNo,
      atUtc: dispatch.updatedAtUtc,
      by: optionalString(payload, 'receivedBy'),
      eventId: meta.eventId,
      deviceId: meta.deviceId,
    };
    dispatch.audit.push(auditEntry);
    dispatch.auditHistory.push(auditEntry);
    await dispatch.save();
    // Deliberately does not update StoreInvoice creditBilling or payments.
  }

  async applyChargeVoided(
    meta: OutboundDispatchEventMeta,
    payload: Record<string, unknown>,
  ): Promise<void> {
    const receiptNo = requiredString(payload, 'receiptNo');
    const reason =
      optionalString(payload, 'voidReason') ??
      optionalString(payload, 'reason') ??
      'Outbound dispatch charge voided';
    const voidedAtUtc = optionalString(payload, 'voidedAtUtc') ?? new Date().toISOString();
    await this.paymentReceiptModel.updateOne(
      { storeId: meta.storeId, receiptNo },
      {
        $set: {
          payload: {
            ...payload,
            receiptNo,
            status: 'void',
            voidReason: reason,
            voidedAtUtc,
          },
        },
      },
    );

    const dispatchNo = optionalString(payload, 'dispatchNo');
    if (!dispatchNo) return;
    const dispatch = await this.dispatchModel.findOne({ storeCode: meta.storeId, dispatchNo });
    if (!dispatch || dispatch.appliedEventIds.includes(meta.eventId)) return;
    dispatch.chargeReceiptNo = receiptNo;
    dispatch.chargeReceipt = {
      ...payload,
      receiptNo,
      status: 'void',
      voidReason: reason,
      voidedAtUtc,
    };
    dispatch.appliedEventIds.push(meta.eventId);
    await dispatch.save();
  }

  async applyCancelled(
    meta: OutboundDispatchEventMeta,
    payload: Record<string, unknown>,
  ): Promise<void> {
    const dispatch = await this.requireDispatch(meta.storeId, requiredString(payload, 'dispatchNo'));
    if (dispatch.appliedEventIds.includes(meta.eventId)) return;
    await this.changeStatus(dispatch, meta, payload, 'Cancelled');
  }

  async list(filters: {
    storeCode?: string;
    businessDate?: string;
    status?: string;
    batchNo?: string;
    limit?: number;
  }) {
    const query: Record<string, unknown> = {};
    if (filters.storeCode?.trim()) query.storeCode = filters.storeCode.trim();
    if (filters.businessDate?.trim()) query.businessDate = filters.businessDate.trim();
    if (filters.status?.trim()) query.status = normalizeOutboundDispatchStatus(filters.status);
    if (filters.batchNo?.trim()) query.batchNo = filters.batchNo.trim();
    const limit = Math.min(500, Math.max(1, filters.limit ?? 100));
    return this.dispatchModel.find(query).sort({ updatedAt: -1 }).limit(limit).lean();
  }

  async get(storeCode: string, dispatchNo: string) {
    const store = storeCode?.trim();
    const no = dispatchNo?.trim();
    if (!store) throw new BadRequestException('storeCode is required');
    if (!no) throw new BadRequestException('dispatchNo is required');
    const dispatch = await this.dispatchModel.findOne({ storeCode: store, dispatchNo: no }).lean();
    if (!dispatch) throw new NotFoundException(`Dispatch '${no}' not found`);
    return dispatch;
  }

  async getActiveByBill(storeCode: string, billNo: string) {
    const store = storeCode?.trim();
    const bill = billNo?.trim();
    if (!store) throw new BadRequestException('storeCode is required');
    if (!bill) throw new BadRequestException('billNo is required');
    return this.dispatchModel
      .findOne({
        storeCode: store,
        billNo: bill,
        status: { $in: ACTIVE_OUTBOUND_DISPATCH_STATUSES },
      })
      .lean();
  }

  private async requireDispatch(storeCode: string, dispatchNo: string) {
    const dispatch = await this.dispatchModel.findOne({ storeCode, dispatchNo });
    if (!dispatch) throw new NotFoundException(`Dispatch '${dispatchNo}' not found`);
    return dispatch;
  }

  private async changeStatus(
    dispatch: OutboundDispatchDocument,
    meta: OutboundDispatchEventMeta,
    payload: Record<string, unknown>,
    next: OutboundDispatchStatus,
  ) {
    assertOutboundDispatchTransition(dispatch.status, next);
    if (dispatch.status === next) {
      dispatch.appliedEventIds.push(meta.eventId);
      await dispatch.save();
      return;
    }
    const previous = dispatch.status;
    dispatch.status = next;
    dispatch.active = isActiveOutboundDispatchStatus(next);
    dispatch.revision += 1;
    dispatch.updatedAtUtc = new Date().toISOString();
    if (next === 'Cancelled') dispatch.cancelledAtUtc = dispatch.updatedAtUtc;
    dispatch.appliedEventIds.push(meta.eventId);
    const auditEntry = {
      action: next === 'Cancelled' ? 'Cancelled' : 'StatusChanged',
      fromStatus: previous,
      toStatus: next,
      reason: optionalString(payload, 'reason') ?? optionalString(payload, 'cancelReason'),
      atUtc: dispatch.updatedAtUtc,
      by: optionalString(payload, 'updatedBy') ?? optionalString(payload, 'cancelledBy'),
      eventId: meta.eventId,
      deviceId: meta.deviceId,
    };
    dispatch.audit.push(auditEntry);
    dispatch.auditHistory.push(auditEntry);
    if (next === 'Cancelled') await this.voidLinkedRecords(dispatch, meta, payload);
    await dispatch.save();
    await this.updateBillDispatchSnapshot(dispatch);
  }

  private async voidLinkedRecords(
    dispatch: OutboundDispatchDocument,
    meta: OutboundDispatchEventMeta,
    payload: Record<string, unknown>,
  ) {
    const normalized = normalizeOutboundDispatchPayload(payload);
    const reason =
      optionalString(payload, 'reason') ??
      optionalString(payload, 'cancelReason') ??
      optionalString(payload, 'statusReason') ??
      'Outbound dispatch cancelled';
    const now =
      optionalString(payload, 'voidedAtUtc') ??
      optionalString(normalized.chargeReceipt ?? {}, 'voidedAtUtc') ??
      new Date().toISOString();

    const receiptNo =
      optionalString(normalized.chargeReceipt ?? {}, 'receiptNo') ??
      dispatch.chargeReceiptNo ??
      optionalString(dispatch.chargeReceipt ?? {}, 'receiptNo');
    if (receiptNo) {
      const supplied = normalized.chargeReceipt ?? dispatch.chargeReceipt ?? {};
      const voidedReceipt = {
        ...supplied,
        receiptNo,
        status: 'void',
        voidReason: optionalString(supplied, 'voidReason') ?? reason,
        voidedAtUtc: optionalString(supplied, 'voidedAtUtc') ?? now,
        dispatchNo: dispatch.dispatchNo,
        billNo: dispatch.billNo,
      };
      await this.paymentReceiptModel.updateOne(
        { storeId: meta.storeId, receiptNo },
        { $set: { payload: voidedReceipt } },
      );
      dispatch.chargeReceiptNo = receiptNo;
      dispatch.chargeReceipt = voidedReceipt;
    }

    const linkedExpense = normalized.linkedExpense ?? dispatch.linkedExpense ?? dispatch.expense;
    const expenseNo =
      optionalString(linkedExpense ?? {}, 'expenseNo') ?? dispatch.expenseNo;
    if (expenseNo) {
      const existing = await this.dailyExpenseModel.findOne({
        storeId: meta.storeId,
        expenseNo,
      });
      const current = (existing?.payload ?? {}) as Record<string, unknown>;
      const currentRevision = Math.max(
        1,
        Number(current.revision ?? current.version) || 1,
      );
      const alreadyVoid = String(current.status ?? '').toLowerCase() === 'void';
      const nextRevision = alreadyVoid ? currentRevision : currentRevision + 1;
      const voidedExpense = {
        ...current,
        ...(linkedExpense ?? {}),
        expenseNo,
        status: 'void',
        voidReason: optionalString(linkedExpense ?? {}, 'voidReason') ?? reason,
        voidedAtUtc: optionalString(linkedExpense ?? {}, 'voidedAtUtc') ?? now,
        dispatchNo: dispatch.dispatchNo,
        outboundDispatchNo: dispatch.dispatchNo,
        billNo: dispatch.billNo,
        revision: nextRevision,
        version: nextRevision,
      };
      if (existing) {
        existing.payload = voidedExpense;
        existing.appliedEventIds = [...new Set([...existing.appliedEventIds, meta.eventId])];
        await existing.save();
      }
      dispatch.expenseNo = expenseNo;
      dispatch.expense = voidedExpense;
      dispatch.linkedExpense = voidedExpense;
    }
  }

  private async persistLinkedExpense(
    meta: OutboundDispatchEventMeta,
    dispatchNo: string,
    billNo: string,
    expense: Record<string, unknown>,
  ) {
    const expenseNo = requiredString(expense, 'expenseNo');
    const existing = await this.dailyExpenseModel.findOne({
      storeId: meta.storeId,
      expenseNo,
    });
    if (existing) {
      const current = (existing.payload ?? {}) as Record<string, unknown>;
      existing.payload = {
        ...current,
        ...expense,
        dispatchNo,
        outboundDispatchNo: dispatchNo,
        billNo,
        expensePurpose: 'outbound_dispatch',
      };
      existing.appliedEventIds = [...new Set([...existing.appliedEventIds, meta.eventId])];
      await existing.save();
      return;
    }
    const normalized = normalizeDailyExpensePayload({
      ...expense,
      dispatchNo,
      outboundDispatchNo: dispatchNo,
      billNo,
      expensePurpose: 'outbound_dispatch',
    });
    await this.dailyExpenseModel.create({
      storeId: meta.storeId,
      expenseNo,
      sourceEventId: `${meta.eventId}:expense`,
      deviceId: meta.deviceId,
      appliedEventIds: [meta.eventId],
      payload: normalized,
    });
  }

  private async updateBillDispatchSnapshot(dispatch: OutboundDispatchDocument): Promise<void> {
    const invoice = await this.invoiceModel.findOne({
      storeId: dispatch.storeCode,
      invoiceNo: dispatch.billNo,
    });
    if (!invoice) return;

    invoice.payload = {
      ...(invoice.payload ?? {}),
      dispatch: {
        dispatchNo: dispatch.dispatchNo,
        status: dispatch.status,
        carrierType: dispatch.carrierType,
        feePayer: dispatch.feePayer,
        updatedAtUtc: dispatch.updatedAtUtc,
      },
    };
    invoice.markModified('payload');
    await invoice.save();
  }
}
