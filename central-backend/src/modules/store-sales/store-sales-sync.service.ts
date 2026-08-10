import { BadRequestException, ConflictException, Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { StoreDailyExpense, StoreDailyExpenseDocument } from './schemas/store-daily-expense.schema';
import { StoreDayClose, StoreDayCloseDocument } from './schemas/store-day-close.schema';
import { StoreCashMovement, StoreCashMovementDocument } from './schemas/store-cash-movement.schema';
import { StoreAdjustment, StoreAdjustmentDocument } from './schemas/store-adjustment.schema';
import { StoreCreditNoteCashout, StoreCreditNoteCashoutDocument } from './schemas/store-credit-note-cashout.schema';
import { StoreCreditNote, StoreCreditNoteDocument } from './schemas/store-credit-note.schema';
import { StoreInvoice, StoreInvoiceDocument } from './schemas/store-invoice.schema';
import { StorePaymentReceipt, StorePaymentReceiptDocument } from './schemas/store-payment-receipt.schema';
import { StoreGatewayPayment, StoreGatewayPaymentDocument } from './schemas/store-gateway-payment.schema';
import { StoreQuotation, StoreQuotationDocument } from './schemas/store-quotation.schema';
import { StoreSaleReturn, StoreSaleReturnDocument } from './schemas/store-sale-return.schema';
import { StoreSalesInventoryService } from './store-sales-inventory.service';
import { readNumber } from '../dashboard/store-sales-payload.util';
import { normalizeDailyExpensePayload } from './daily-expense-payload';

export type StoreSyncEventMeta = {
  eventId: string;
  storeId: string;
  deviceId: string;
};

@Injectable()
export class StoreSalesSyncService {
  constructor(
    @InjectModel(StoreInvoice.name) private readonly invoiceModel: Model<StoreInvoiceDocument>,
    @InjectModel(StoreSaleReturn.name) private readonly returnModel: Model<StoreSaleReturnDocument>,
    @InjectModel(StoreAdjustment.name) private readonly adjustmentModel: Model<StoreAdjustmentDocument>,
    @InjectModel(StoreDailyExpense.name) private readonly dailyExpenseModel: Model<StoreDailyExpenseDocument>,
    @InjectModel(StoreCreditNote.name) private readonly creditNoteModel: Model<StoreCreditNoteDocument>,
    @InjectModel(StoreCreditNoteCashout.name)
    private readonly creditNoteCashoutModel: Model<StoreCreditNoteCashoutDocument>,
    @InjectModel(StoreDayClose.name) private readonly dayCloseModel: Model<StoreDayCloseDocument>,
    @InjectModel(StoreCashMovement.name) private readonly cashMovementModel: Model<StoreCashMovementDocument>,
    @InjectModel(StoreQuotation.name) private readonly quotationModel: Model<StoreQuotationDocument>,
    @InjectModel(StorePaymentReceipt.name)
    private readonly paymentReceiptModel: Model<StorePaymentReceiptDocument>,
    @InjectModel(StoreGatewayPayment.name)
    private readonly gatewayPaymentModel: Model<StoreGatewayPaymentDocument>,
    private readonly storeSalesInventoryService: StoreSalesInventoryService,
  ) {}

  private requireString(payload: Record<string, unknown>, key: string): string {
    const v = payload[key];
    if (typeof v !== 'string' || !v.trim()) {
      throw new BadRequestException(`${key} is required`);
    }
    return v.trim();
  }

  private optionalString(payload: Record<string, unknown>, key: string): string | undefined {
    const v = payload[key];
    if (v === undefined || v === null) return undefined;
    const s = String(v).trim();
    return s === '' ? undefined : s;
  }

  private requireNumber(payload: Record<string, unknown>, key: string): number {
    const v = payload[key];
    const n = typeof v === 'number' ? v : Number(v);
    if (!Number.isFinite(n)) throw new BadRequestException(`${key} must be a number`);
    return n;
  }

  async applyInvoiceCreated(meta: StoreSyncEventMeta, payload: Record<string, unknown>): Promise<void> {
    const existingByEvent = await this.invoiceModel.findOne({ sourceEventId: meta.eventId }).lean();
    if (existingByEvent) return;

    const invoiceNo =
      this.optionalString(payload, 'billNo') ?? this.optionalString(payload, 'invoiceNo');
    if (!invoiceNo) throw new BadRequestException('billNo or invoiceNo is required');

    const duplicate = await this.invoiceModel.findOne({ storeId: meta.storeId, invoiceNo }).lean();
    if (duplicate) {
      throw new ConflictException(
        `Invoice '${invoiceNo}' already exists for store '${meta.storeId}'`,
      );
    }

    const posCounter = this.optionalString(payload, 'posCounter');
    try {
      await this.invoiceModel.create({
        storeId: meta.storeId,
        invoiceNo,
        sourceEventId: meta.eventId,
        deviceId: meta.deviceId,
        posCounter,
        payload,
      });
      await this.storeSalesInventoryService.postInvoiceLedger(meta, payload);
    } catch (err: unknown) {
      const dup =
        err && typeof err === 'object' && 'code' in err && (err as { code?: number }).code === 11000;
      if (dup) {
        const again = await this.invoiceModel.findOne({ sourceEventId: meta.eventId }).lean();
        if (again) return;
        throw new ConflictException(
          `Invoice '${invoiceNo}' already exists for store '${meta.storeId}'`,
        );
      }
      throw err;
    }
  }

  async applyInvoiceDeleted(meta: StoreSyncEventMeta, payload: Record<string, unknown>): Promise<void> {
    const invoiceNo =
      this.optionalString(payload, 'billNo') ?? this.optionalString(payload, 'invoiceNo');
    if (!invoiceNo) throw new BadRequestException('billNo or invoiceNo is required');

    const existing = await this.invoiceModel.findOne({ storeId: meta.storeId, invoiceNo }).lean();
    if (!existing) {
      // Idempotent: invoice already gone (or never synced to central).
      return;
    }

    await this.invoiceModel.deleteOne({ storeId: meta.storeId, invoiceNo });
    await this.storeSalesInventoryService.postInvoiceDeletedLedger(meta, payload);
  }

  async applySaleReturn(
    meta: StoreSyncEventMeta,
    payload: Record<string, unknown>,
    kind: 'return' | 'exchange',
  ): Promise<void> {
    const existing = await this.returnModel.findOne({ sourceEventId: meta.eventId }).lean();
    if (existing) return;

    const returnNo = this.requireString(payload, 'returnNo');
    const duplicate = await this.returnModel.findOne({ storeId: meta.storeId, returnNo }).lean();
    if (duplicate) {
      throw new ConflictException(
        `Return '${returnNo}' already exists for store '${meta.storeId}'`,
      );
    }

    try {
      await this.returnModel.create({
        storeId: meta.storeId,
        returnNo,
        sourceEventId: meta.eventId,
        deviceId: meta.deviceId,
        kind,
        payload,
      });
      await this.storeSalesInventoryService.postReturnLedger(meta, payload, kind);
    } catch (err: unknown) {
      const dup =
        err && typeof err === 'object' && 'code' in err && (err as { code?: number }).code === 11000;
      if (dup) {
        const again = await this.returnModel.findOne({ sourceEventId: meta.eventId }).lean();
        if (again) return;
        throw new ConflictException(
          `Return '${returnNo}' already exists for store '${meta.storeId}'`,
        );
      }
      throw err;
    }
  }

  async applyAdjustmentCreated(meta: StoreSyncEventMeta, payload: Record<string, unknown>): Promise<void> {
    const existing = await this.adjustmentModel.findOne({ sourceEventId: meta.eventId }).lean();
    if (existing) return;

    const adjustmentNo = this.requireString(payload, 'adjustmentNo');
    const duplicate = await this.adjustmentModel
      .findOne({ storeId: meta.storeId, adjustmentNo })
      .lean();
    if (duplicate) {
      throw new ConflictException(
        `Adjustment '${adjustmentNo}' already exists for store '${meta.storeId}'`,
      );
    }

    try {
      await this.adjustmentModel.create({
        storeId: meta.storeId,
        adjustmentNo,
        sourceEventId: meta.eventId,
        deviceId: meta.deviceId,
        payload,
      });
    } catch (err: unknown) {
      const dup =
        err && typeof err === 'object' && 'code' in err && (err as { code?: number }).code === 11000;
      if (dup) {
        const again = await this.adjustmentModel.findOne({ sourceEventId: meta.eventId }).lean();
        if (again) return;
        throw new ConflictException(
          `Adjustment '${adjustmentNo}' already exists for store '${meta.storeId}'`,
        );
      }
      throw err;
    }
  }

  async applyCreditNoteCreated(meta: StoreSyncEventMeta, payload: Record<string, unknown>): Promise<void> {
    const existing = await this.creditNoteModel.findOne({ createSourceEventId: meta.eventId }).lean();
    if (existing) return;

    const creditNoteNo = this.requireString(payload, 'creditNoteNo');
    const amount = this.requireNumber(payload, 'amount');
    const remaining =
      payload.remainingAmount !== undefined
        ? this.requireNumber(payload, 'remainingAmount')
        : amount;

    const duplicate = await this.creditNoteModel.findOne({ storeId: meta.storeId, creditNoteNo });
    if (duplicate) {
      // Upsert amounts for additional credit from a later return against the same CN.
      duplicate.amount = amount;
      duplicate.remainingAmount = remaining;
      if (payload.status === 'available' || remaining > 0) duplicate.status = 'available';
      const returnNo = this.optionalString(payload, 'returnNo');
      if (returnNo) duplicate.returnNo = returnNo;
      await duplicate.save();
      return;
    }

    try {
      await this.creditNoteModel.create({
        storeId: meta.storeId,
        creditNoteNo,
        createSourceEventId: meta.eventId,
        status: 'available',
        amount,
        remainingAmount: remaining,
        totalApplied: 0,
        returnNo: this.optionalString(payload, 'returnNo'),
        originalBillNo: this.optionalString(payload, 'originalBillNo'),
        originalBillDate: this.optionalString(payload, 'originalBillDate'),
        isLegacy: payload.isLegacy === true,
        customerCode: this.optionalString(payload, 'customerCode'),
        customerPhone: this.optionalString(payload, 'customerPhone'),
        customerName: this.optionalString(payload, 'customerName'),
        applications: [],
      });
    } catch (err: unknown) {
      const dup =
        err && typeof err === 'object' && 'code' in err && (err as { code?: number }).code === 11000;
      if (dup) return;
      throw err;
    }
  }

  async applyCreditNoteApplied(meta: StoreSyncEventMeta, payload: Record<string, unknown>): Promise<void> {
    const creditNoteNo = this.requireString(payload, 'creditNoteNo');
    const billNo = this.requireString(payload, 'billNo');
    const amountApplied = this.requireNumber(payload, 'amountApplied');

    const note = await this.creditNoteModel.findOne({
      storeId: meta.storeId,
      creditNoteNo,
    });
    if (!note) throw new BadRequestException(`Credit note '${creditNoteNo}' not found`);

    const already = note.applications?.find((a) => a.sourceEventId === meta.eventId);
    if (already) return;

    if (amountApplied <= 0) throw new BadRequestException('amountApplied must be positive');

    const remainingAfter =
      payload.remainingAmount !== undefined
        ? this.requireNumber(payload, 'remainingAmount')
        : Math.max(0, note.remainingAmount - amountApplied);

    const status =
      this.optionalString(payload, 'status') ??
      (remainingAfter <= 0 ? 'consumed' : 'available');

    note.totalApplied = (note.totalApplied ?? 0) + amountApplied;
    note.remainingAmount = remainingAfter;
    note.status = status === 'consumed' ? 'consumed' : 'available';
    note.lastAppliedBillNo = billNo;
    if (note.status === 'consumed') note.consumedBillNo = billNo;

    note.applications = note.applications ?? [];
    note.applications.push({
      billNo,
      amountApplied,
      appliedAt: new Date().toISOString(),
      sourceEventId: meta.eventId,
    });

    await note.save();
  }

  async applyDailyExpenseCreated(meta: StoreSyncEventMeta, payload: Record<string, unknown>): Promise<void> {
    const existing = await this.dailyExpenseModel.findOne({ sourceEventId: meta.eventId }).lean();
    if (existing) return;

    const normalized = normalizeDailyExpensePayload(payload);
    const expenseNo = String(normalized.expenseNo);

    const duplicate = await this.dailyExpenseModel
      .findOne({ storeId: meta.storeId, expenseNo })
      .lean();
    if (duplicate) {
      throw new ConflictException(
        `Expense '${expenseNo}' already exists for store '${meta.storeId}'`,
      );
    }

    try {
      await this.dailyExpenseModel.create({
        storeId: meta.storeId,
        expenseNo,
        sourceEventId: meta.eventId,
        deviceId: meta.deviceId,
        appliedEventIds: [meta.eventId],
        payload: normalized,
      });
    } catch (err: unknown) {
      const dup =
        err && typeof err === 'object' && 'code' in err && (err as { code?: number }).code === 11000;
      if (dup) {
        const again = await this.dailyExpenseModel.findOne({ sourceEventId: meta.eventId }).lean();
        if (again) return;
        throw new ConflictException(
          `Expense '${expenseNo}' already exists for store '${meta.storeId}'`,
        );
      }
      throw err;
    }
  }

  async applyDailyExpenseUpdated(meta: StoreSyncEventMeta, payload: Record<string, unknown>): Promise<void> {
    const expenseNo = this.requireString(payload, 'expenseNo');
    const existing = await this.dailyExpenseModel.findOne({ storeId: meta.storeId, expenseNo }).lean();
    if (!existing) {
      throw new BadRequestException(`Expense '${expenseNo}' not found for store '${meta.storeId}'`);
    }
    if ((existing.appliedEventIds ?? []).includes(meta.eventId)) return;

    const currentPayload = (existing.payload ?? {}) as Record<string, unknown>;
    const currentStatus = (this.optionalString(currentPayload, 'status') ?? 'posted').toLowerCase();
    if (currentStatus === 'void' || currentStatus === 'cancelled') {
      throw new BadRequestException(`Expense '${expenseNo}' is void and cannot be updated`);
    }

    const currentVersion = Math.max(
      1,
      readNumber(currentPayload.version) || readNumber(currentPayload.revision),
    );
    const requestedVersion = readNumber(payload.version) || readNumber(payload.revision);
    if (requestedVersion > 0 && requestedVersion <= currentVersion) {
      await this.dailyExpenseModel.updateOne(
        { storeId: meta.storeId, expenseNo },
        { $addToSet: { appliedEventIds: meta.eventId } },
      );
      return;
    }

    const normalized = normalizeDailyExpensePayload({
      ...currentPayload,
      ...payload,
      expenseNo,
      version: requestedVersion > 0 ? requestedVersion : currentVersion + 1,
      revision: requestedVersion > 0 ? requestedVersion : currentVersion + 1,
      status: currentStatus,
      updatedBy: payload.updatedBy ?? currentPayload.updatedBy,
      updatedAtUtc: payload.updatedAtUtc ?? new Date().toISOString(),
    });

    await this.dailyExpenseModel.updateOne(
      { storeId: meta.storeId, expenseNo, appliedEventIds: { $ne: meta.eventId } },
      {
        $set: {
          deviceId: meta.deviceId,
          payload: normalized,
        },
        $addToSet: { appliedEventIds: meta.eventId },
      },
    );
  }

  async applyDailyExpenseVoided(meta: StoreSyncEventMeta, payload: Record<string, unknown>): Promise<void> {
    const expenseNo = this.requireString(payload, 'expenseNo');
    const voidReason =
      this.optionalString(payload, 'voidReason') ?? this.optionalString(payload, 'reason');
    if (!voidReason) throw new BadRequestException('voidReason is required');

    const existing = await this.dailyExpenseModel.findOne({ storeId: meta.storeId, expenseNo }).lean();
    if (!existing) {
      throw new BadRequestException(`Expense '${expenseNo}' not found for store '${meta.storeId}'`);
    }
    if ((existing.appliedEventIds ?? []).includes(meta.eventId)) return;

    const currentPayload = (existing.payload ?? {}) as Record<string, unknown>;
    const currentStatus = (this.optionalString(currentPayload, 'status') ?? 'posted').toLowerCase();
    if (currentStatus === 'void' || currentStatus === 'cancelled') {
      await this.dailyExpenseModel.updateOne(
        { storeId: meta.storeId, expenseNo },
        { $addToSet: { appliedEventIds: meta.eventId } },
      );
      return;
    }

    const currentVersion = Math.max(
      1,
      readNumber(currentPayload.version) || readNumber(currentPayload.revision),
    );
    const requestedVersion = readNumber(payload.version) || readNumber(payload.revision);
    await this.dailyExpenseModel.updateOne(
      { storeId: meta.storeId, expenseNo, appliedEventIds: { $ne: meta.eventId } },
      {
        $set: {
          deviceId: meta.deviceId,
          payload: {
            ...currentPayload,
            ...payload,
            expenseNo,
            status: 'void',
            version: requestedVersion > currentVersion ? requestedVersion : currentVersion + 1,
            revision: requestedVersion > currentVersion ? requestedVersion : currentVersion + 1,
            voidReason,
            voidedBy: payload.voidedBy ?? payload.updatedBy ?? currentPayload.voidedBy,
            voidedAtUtc: payload.voidedAtUtc ?? new Date().toISOString(),
          },
        },
        $addToSet: { appliedEventIds: meta.eventId },
      },
    );
  }

  async applyCreditNoteCashedOut(meta: StoreSyncEventMeta, payload: Record<string, unknown>): Promise<void> {
    const existingByEvent = await this.creditNoteCashoutModel
      .findOne({ createSourceEventId: meta.eventId })
      .lean();
    if (existingByEvent) return;

    const cashoutNo = this.requireString(payload, 'cashoutNo');
    const creditNoteNo = this.requireString(payload, 'creditNoteNo');
    const cashRefunded = this.requireNumber(payload, 'cashRefunded');
    if (cashRefunded <= 0) throw new BadRequestException('cashRefunded must be positive');

    const duplicate = await this.creditNoteCashoutModel.findOne({ storeId: meta.storeId, cashoutNo }).lean();
    if (duplicate) return;

    const billNo = this.optionalString(payload, 'billNo') ?? 'CASHOUT';
    const note = await this.creditNoteModel.findOne({ storeId: meta.storeId, creditNoteNo });

    const remainingBefore =
      payload.remainingBefore !== undefined
        ? this.requireNumber(payload, 'remainingBefore')
        : note?.remainingAmount ?? cashRefunded;
    const remainingAfter =
      payload.remainingAfter !== undefined
        ? this.requireNumber(payload, 'remainingAfter')
        : payload.remainingAmount !== undefined
          ? this.requireNumber(payload, 'remainingAmount')
          : Math.max(0, remainingBefore - cashRefunded);

    if (note) {
      const creditNoteStatus =
        this.optionalString(payload, 'creditNoteStatus') ??
        this.optionalString(payload, 'status') ??
        (remainingAfter <= 0 ? 'consumed' : 'available');

      note.totalApplied = (note.totalApplied ?? 0) + cashRefunded;
      note.remainingAmount = remainingAfter;
      note.status = creditNoteStatus === 'consumed' ? 'consumed' : 'available';
      note.lastAppliedBillNo = billNo;
      if (note.status === 'consumed') note.consumedBillNo = billNo;

      note.applications = note.applications ?? [];
      note.applications.push({
        billNo,
        amountApplied: cashRefunded,
        appliedAt: new Date().toISOString(),
        sourceEventId: meta.eventId,
      });

      await note.save();
    }

    await this.creditNoteCashoutModel.create({
      storeId: meta.storeId,
      cashoutNo,
      createSourceEventId: meta.eventId,
      creditNoteNo,
      billNo,
      cashRefunded,
      remainingBefore,
      remainingAfter,
      posCounter: this.optionalString(payload, 'posCounter'),
      customerCode: this.optionalString(payload, 'customerCode'),
      customerPhone: this.optionalString(payload, 'customerPhone'),
      customerName: this.optionalString(payload, 'customerName'),
      // Cashout rows are always posted; payload.status may carry credit-note lifecycle (consumed/available).
      status: 'posted',
      createdAtUtc: this.optionalString(payload, 'createdAtUtc') ?? new Date().toISOString(),
    });
  }

  async applyDaySessionOpened(meta: StoreSyncEventMeta, payload: Record<string, unknown>): Promise<void> {
    await this.upsertDaySessionRecord(meta, payload, false);
  }

  async applyDaySessionClosed(meta: StoreSyncEventMeta, payload: Record<string, unknown>): Promise<void> {
    await this.upsertDaySessionRecord(meta, payload, true);
  }

  async applyCashMovementCreated(meta: StoreSyncEventMeta, payload: Record<string, unknown>): Promise<void> {
    const existing = await this.cashMovementModel.findOne({ sourceEventId: meta.eventId }).lean();
    if (existing) return;

    const movementNo = this.requireString(payload, 'movementNo');
    const businessDate = this.requireString(payload, 'businessDate');
    if (!/^\d{4}-\d{2}-\d{2}$/.test(businessDate)) {
      throw new BadRequestException('businessDate must be YYYY-MM-DD');
    }

    const amount = this.requireNumber(payload, 'amount');
    if (amount <= 0) throw new BadRequestException('amount must be positive');

    const duplicate = await this.cashMovementModel
      .findOne({ storeId: meta.storeId, movementNo })
      .lean();
    if (duplicate) {
      throw new ConflictException(
        `Cash movement '${movementNo}' already exists for store '${meta.storeId}'`,
      );
    }

    try {
      await this.cashMovementModel.create({
        storeId: meta.storeId,
        movementNo,
        sourceEventId: meta.eventId,
        deviceId: meta.deviceId,
        payload: { ...payload, movementNo, businessDate, amount },
      });
    } catch (err: unknown) {
      const dup =
        err && typeof err === 'object' && 'code' in err && (err as { code?: number }).code === 11000;
      if (dup) {
        const again = await this.cashMovementModel.findOne({ sourceEventId: meta.eventId }).lean();
        if (again) return;
        throw new ConflictException(
          `Cash movement '${movementNo}' already exists for store '${meta.storeId}'`,
        );
      }
      throw err;
    }
  }

  private async upsertDaySessionRecord(
    meta: StoreSyncEventMeta,
    payload: Record<string, unknown>,
    requireClose: boolean,
  ): Promise<void> {
    const existingByEvent = await this.dayCloseModel.findOne({ sourceEventId: meta.eventId }).lean();
    if (existingByEvent) return;

    const businessDate = this.requireString(payload, 'businessDate');
    if (!/^\d{4}-\d{2}-\d{2}$/.test(businessDate)) {
      throw new BadRequestException('businessDate must be YYYY-MM-DD');
    }

    const posCounter = this.requireString(payload, 'posCounter');
    const status = this.requireString(payload, 'status');
    if (requireClose && status !== 'closed') {
      throw new BadRequestException('DaySessionClosed requires status closed');
    }

    const mergedPayload: Record<string, unknown> = {
      ...payload,
      businessDate,
      posCounter,
      status,
    };

    const duplicate = await this.dayCloseModel
      .findOne({ storeId: meta.storeId, businessDate, posCounter })
      .lean();
    if (duplicate) {
      const priorStatus = String(
        (duplicate.payload as Record<string, unknown> | undefined)?.status ?? '',
      ).toLowerCase();

      if (requireClose && priorStatus === 'closed') {
        throw new ConflictException(
          `Day close already exists for store '${meta.storeId}' counter '${posCounter}' on ${businessDate}`,
        );
      }

      // Online/offline close after open: update the existing open row in place.
      if (requireClose) {
        await this.dayCloseModel.updateOne(
          { storeId: meta.storeId, businessDate, posCounter },
          {
            $set: {
              sourceEventId: meta.eventId,
              deviceId: meta.deviceId,
              payload: {
                ...((duplicate.payload as Record<string, unknown> | undefined) ?? {}),
                ...mergedPayload,
              },
            },
          },
        );
        return;
      }

      // DaySessionOpened when a row already exists — idempotent no-op.
      return;
    }

    try {
      await this.dayCloseModel.create({
        storeId: meta.storeId,
        businessDate,
        posCounter,
        sourceEventId: meta.eventId,
        deviceId: meta.deviceId,
        payload: mergedPayload,
      });
    } catch (err: unknown) {
      const dup =
        err && typeof err === 'object' && 'code' in err && (err as { code?: number }).code === 11000;
      if (dup) {
        const again = await this.dayCloseModel.findOne({ sourceEventId: meta.eventId }).lean();
        if (again) return;
        if (requireClose) {
          // Race: open created between find and create — retry as update.
          const raced = await this.dayCloseModel
            .findOne({ storeId: meta.storeId, businessDate, posCounter })
            .lean();
          if (raced) {
            const racedStatus = String(
              (raced.payload as Record<string, unknown> | undefined)?.status ?? '',
            ).toLowerCase();
            if (racedStatus === 'closed') {
              throw new ConflictException(
                `Day close already exists for store '${meta.storeId}' counter '${posCounter}' on ${businessDate}`,
              );
            }
            await this.dayCloseModel.updateOne(
              { storeId: meta.storeId, businessDate, posCounter },
              {
                $set: {
                  sourceEventId: meta.eventId,
                  deviceId: meta.deviceId,
                  payload: {
                    ...((raced.payload as Record<string, unknown> | undefined) ?? {}),
                    ...mergedPayload,
                  },
                },
              },
            );
            return;
          }
          throw new ConflictException(
            `Day close already exists for store '${meta.storeId}' counter '${posCounter}' on ${businessDate}`,
          );
        }
        return;
      }
      throw err;
    }
  }

  async applyInvoiceCodPaymentReceived(
    meta: StoreSyncEventMeta,
    payload: Record<string, unknown>,
  ): Promise<void> {
    const billNo =
      this.optionalString(payload, 'billNo') ?? this.optionalString(payload, 'invoiceNo');
    if (!billNo) throw new BadRequestException('billNo is required');

    const invoice = await this.invoiceModel
      .findOne({ storeId: meta.storeId, invoiceNo: billNo })
      .lean();
    if (!invoice) {
      throw new BadRequestException(
        `Invoice '${billNo}' not found for store '${meta.storeId}'`,
      );
    }

    const currentPayload = (invoice.payload ?? {}) as Record<string, unknown>;
    const appliedEventIds = Array.isArray(currentPayload.codPaymentEventIds)
      ? (currentPayload.codPaymentEventIds as unknown[]).map((id) => String(id))
      : [];
    if (appliedEventIds.includes(meta.eventId)) return;

    const mergedPayload: Record<string, unknown> = {
      ...currentPayload,
      salesChannel: payload.salesChannel ?? currentPayload.salesChannel,
      onlineCod: payload.onlineCod ?? currentPayload.onlineCod,
      payments: payload.payments ?? currentPayload.payments,
      paymentMode: payload.paymentMode ?? currentPayload.paymentMode,
      codPaymentEventIds: [...appliedEventIds, meta.eventId],
    };

    await this.invoiceModel.updateOne(
      { storeId: meta.storeId, invoiceNo: billNo },
      { $set: { payload: mergedPayload } },
    );
  }

  async applyQuotationUpserted(meta: StoreSyncEventMeta, payload: Record<string, unknown>): Promise<void> {
    const existingByEvent = await this.quotationModel.findOne({ sourceEventId: meta.eventId }).lean();
    if (existingByEvent) return;

    const quotationNo = this.requireString(payload, 'quotationNo');
    const status = this.optionalString(payload, 'status') ?? 'open';

    const duplicate = await this.quotationModel
      .findOne({ storeId: meta.storeId, quotationNo })
      .lean();
    if (duplicate) {
      await this.quotationModel.updateOne(
        { storeId: meta.storeId, quotationNo },
        {
          $set: {
            status,
            posCounter: this.optionalString(payload, 'posCounter'),
            payload,
          },
        },
      );
      return;
    }

    try {
      await this.quotationModel.create({
        storeId: meta.storeId,
        quotationNo,
        sourceEventId: meta.eventId,
        deviceId: meta.deviceId,
        posCounter: this.optionalString(payload, 'posCounter'),
        status,
        payload,
      });
    } catch (err: unknown) {
      const dup =
        err && typeof err === 'object' && 'code' in err && (err as { code?: number }).code === 11000;
      if (dup) {
        const again = await this.quotationModel.findOne({ sourceEventId: meta.eventId }).lean();
        if (again) return;
        await this.quotationModel.updateOne(
          { storeId: meta.storeId, quotationNo },
          {
            $set: {
              status,
              posCounter: this.optionalString(payload, 'posCounter'),
              payload,
            },
          },
        );
        return;
      }
      throw err;
    }
  }

  async applyQuotationConverted(meta: StoreSyncEventMeta, payload: Record<string, unknown>): Promise<void> {
    const quotationNo = this.requireString(payload, 'quotationNo');
    const convertedBillNo =
      this.optionalString(payload, 'convertedBillNo') ?? this.optionalString(payload, 'billNo');
    if (!convertedBillNo) throw new BadRequestException('convertedBillNo or billNo is required');

    const existing = await this.quotationModel.findOne({ storeId: meta.storeId, quotationNo });
    if (!existing) {
      await this.applyQuotationUpserted(meta, {
        ...payload,
        quotationNo,
        status: 'converted',
        convertedBillNo,
      });
      return;
    }

    const appliedEventIds = Array.isArray(existing.payload?.convertedEventIds)
      ? (existing.payload.convertedEventIds as unknown[]).map((id) => String(id))
      : [];
    if (appliedEventIds.includes(meta.eventId)) return;

    const mergedPayload: Record<string, unknown> = {
      ...(existing.payload ?? {}),
      ...payload,
      quotationNo,
      status: 'converted',
      convertedBillNo,
      convertedEventIds: [...appliedEventIds, meta.eventId],
    };

    existing.status = 'converted';
    existing.convertedBillNo = convertedBillNo;
    existing.payload = mergedPayload;
    await existing.save();
  }

  async applyQuotationCancelled(meta: StoreSyncEventMeta, payload: Record<string, unknown>): Promise<void> {
    const quotationNo = this.requireString(payload, 'quotationNo');

    const existing = await this.quotationModel.findOne({ storeId: meta.storeId, quotationNo });
    if (!existing) {
      await this.applyQuotationUpserted(meta, { ...payload, quotationNo, status: 'cancelled' });
      return;
    }

    const appliedEventIds = Array.isArray(existing.payload?.cancelledEventIds)
      ? (existing.payload.cancelledEventIds as unknown[]).map((id) => String(id))
      : [];
    if (appliedEventIds.includes(meta.eventId)) return;

    const mergedPayload: Record<string, unknown> = {
      ...(existing.payload ?? {}),
      ...payload,
      quotationNo,
      status: 'cancelled',
      cancelledEventIds: [...appliedEventIds, meta.eventId],
    };

    existing.status = 'cancelled';
    existing.payload = mergedPayload;
    await existing.save();
  }

  async applyInvoiceCreditPaymentReceived(
    meta: StoreSyncEventMeta,
    payload: Record<string, unknown>,
  ): Promise<void> {
    const billNo =
      this.optionalString(payload, 'billNo') ?? this.optionalString(payload, 'invoiceNo');
    if (!billNo) throw new BadRequestException('billNo is required');

    const invoice = await this.invoiceModel
      .findOne({ storeId: meta.storeId, invoiceNo: billNo })
      .lean();
    if (!invoice) {
      throw new BadRequestException(
        `Invoice '${billNo}' not found for store '${meta.storeId}'`,
      );
    }

    const currentPayload = (invoice.payload ?? {}) as Record<string, unknown>;
    const appliedEventIds = Array.isArray(currentPayload.creditPaymentEventIds)
      ? (currentPayload.creditPaymentEventIds as unknown[]).map((id) => String(id))
      : [];
    if (appliedEventIds.includes(meta.eventId)) return;

    const receipt = payload.receipt;
    if (receipt && typeof receipt === 'object') {
      const receiptRow = receipt as Record<string, unknown>;
      const receiptNo = this.optionalString(receiptRow, 'receiptNo');
      if (receiptNo) {
        const existingReceipt = await this.paymentReceiptModel
          .findOne({ sourceEventId: meta.eventId })
          .lean();
        if (!existingReceipt) {
          try {
            await this.paymentReceiptModel.create({
              storeId: meta.storeId,
              receiptNo,
              billNo,
              sourceEventId: meta.eventId,
              deviceId: meta.deviceId,
              payload: receiptRow,
            });
          } catch (err: unknown) {
            const dup =
              err &&
              typeof err === 'object' &&
              'code' in err &&
              (err as { code?: number }).code === 11000;
            if (!dup) throw err;
          }
        }
      }
    }

    const mergedPayload: Record<string, unknown> = {
      ...currentPayload,
      creditBilling: payload.creditBilling ?? currentPayload.creditBilling,
      payments: payload.payments ?? currentPayload.payments,
      paymentMode: payload.paymentMode ?? currentPayload.paymentMode,
      creditPaymentEventIds: [...appliedEventIds, meta.eventId],
    };

    await this.invoiceModel.updateOne(
      { storeId: meta.storeId, invoiceNo: billNo },
      { $set: { payload: mergedPayload } },
    );
  }

  async applyInvoiceStockExceptionsApproved(
    meta: StoreSyncEventMeta,
    payload: Record<string, unknown>,
  ): Promise<void> {
    const billNo =
      this.optionalString(payload, 'billNo') ?? this.optionalString(payload, 'invoiceNo');
    if (!billNo) throw new BadRequestException('billNo is required');

    const invoice = await this.invoiceModel.findOne({ storeId: meta.storeId, invoiceNo: billNo }).lean();
    if (!invoice) {
      throw new BadRequestException(`Invoice '${billNo}' not found for store '${meta.storeId}'`);
    }

    const currentPayload = { ...(invoice.payload ?? {}) } as Record<string, unknown>;
    const appliedIds = Array.isArray(currentPayload.stockExceptionApproveEventIds)
      ? (currentPayload.stockExceptionApproveEventIds as unknown[]).map((id) => String(id))
      : [];
    if (appliedIds.includes(meta.eventId)) return;

    const exceptions = Array.isArray(currentPayload.stockExceptions)
      ? [...(currentPayload.stockExceptions as unknown[])]
      : [];
    const pending = exceptions.filter((item) => {
      if (!item || typeof item !== 'object') return false;
      return (item as Record<string, unknown>).stockDecremented !== true;
    });
    if (pending.length === 0) {
      throw new BadRequestException('No pending stock exceptions to approve');
    }

    await this.storeSalesInventoryService.postStockExceptionApproveLedger(meta, {
      ...currentPayload,
      billNo,
      invoiceNo: billNo,
    });

    const approvedAt = new Date().toISOString();
    const approvedBy =
      this.optionalString(payload, 'approvedBy') ??
      this.optionalString(payload, 'approvedByUser') ??
      '';
    const updatedExceptions = exceptions.map((item) => {
      if (!item || typeof item !== 'object') return item;
      const row = { ...(item as Record<string, unknown>) };
      if (row.stockDecremented === true) return row;
      return {
        ...row,
        stockDecremented: true,
        approvedAtUtc: approvedAt,
        approvedBy,
      };
    });

    const mergedPayload: Record<string, unknown> = {
      ...currentPayload,
      stockExceptions: updatedExceptions,
      stockExceptionApproveEventIds: [...appliedIds, meta.eventId],
    };

    await this.invoiceModel.updateOne(
      { storeId: meta.storeId, invoiceNo: billNo },
      { $set: { payload: mergedPayload } },
    );
  }

  async applyInvoiceWhatsAppUpdated(
    meta: StoreSyncEventMeta,
    payload: Record<string, unknown>,
  ): Promise<void> {
    const billNo =
      this.optionalString(payload, 'billNo') ?? this.optionalString(payload, 'invoiceNo');
    if (!billNo) throw new BadRequestException('billNo is required');

    const invoice = await this.invoiceModel.findOne({ storeId: meta.storeId, invoiceNo: billNo }).lean();
    if (!invoice) {
      throw new BadRequestException(`Invoice '${billNo}' not found for store '${meta.storeId}'`);
    }

    const currentPayload = { ...(invoice.payload ?? {}) } as Record<string, unknown>;
    const whatsapp =
      payload.whatsapp && typeof payload.whatsapp === 'object'
        ? (payload.whatsapp as Record<string, unknown>)
        : payload;

    const mergedPayload: Record<string, unknown> = {
      ...currentPayload,
      whatsapp: {
        ...((currentPayload.whatsapp && typeof currentPayload.whatsapp === 'object'
          ? currentPayload.whatsapp
          : {}) as Record<string, unknown>),
        ...whatsapp,
        updatedAtUtc: new Date().toISOString(),
        sourceEventId: meta.eventId,
      },
    };

    await this.invoiceModel.updateOne(
      { storeId: meta.storeId, invoiceNo: billNo },
      { $set: { payload: mergedPayload } },
    );
  }

  async applyInvoicePrintAuditAppended(
    meta: StoreSyncEventMeta,
    payload: Record<string, unknown>,
  ): Promise<void> {
    const billNo =
      this.optionalString(payload, 'billNo') ?? this.optionalString(payload, 'invoiceNo');
    if (!billNo) throw new BadRequestException('billNo is required');

    const invoice = await this.invoiceModel.findOne({ storeId: meta.storeId, invoiceNo: billNo }).lean();
    if (!invoice) {
      throw new BadRequestException(`Invoice '${billNo}' not found for store '${meta.storeId}'`);
    }

    const currentPayload = { ...(invoice.payload ?? {}) } as Record<string, unknown>;
    const appliedIds = Array.isArray(currentPayload.printAuditEventIds)
      ? (currentPayload.printAuditEventIds as unknown[]).map((id) => String(id))
      : [];
    if (appliedIds.includes(meta.eventId)) return;

    const entry = {
      kind: this.optionalString(payload, 'kind') ?? 'print',
      printedBy: this.optionalString(payload, 'printedBy') ?? '',
      printedAtUtc: this.optionalString(payload, 'printedAtUtc') ?? new Date().toISOString(),
      deviceId: meta.deviceId,
      posCounter: this.optionalString(payload, 'posCounter') ?? '',
      sourceEventId: meta.eventId,
    };

    const existingAudit = Array.isArray(currentPayload.printAudit)
      ? [...(currentPayload.printAudit as unknown[])]
      : [];
    existingAudit.push(entry);

    const mergedPayload: Record<string, unknown> = {
      ...currentPayload,
      printAudit: existingAudit,
      printAuditEventIds: [...appliedIds, meta.eventId],
    };

    await this.invoiceModel.updateOne(
      { storeId: meta.storeId, invoiceNo: billNo },
      { $set: { payload: mergedPayload } },
    );
  }

  async applyDaySessionCashHandOverPrinted(
    meta: StoreSyncEventMeta,
    payload: Record<string, unknown>,
  ): Promise<void> {
    const businessDate = this.requireString(payload, 'businessDate');
    const posCounter = this.requireString(payload, 'posCounter');
    const printedAt = this.optionalString(payload, 'cashHandOverPrintedAtUtc') ?? new Date().toISOString();

    const session = await this.dayCloseModel
      .findOne({ storeId: meta.storeId, businessDate, posCounter })
      .lean();
    if (!session) {
      throw new BadRequestException(
        `Day session not found for store '${meta.storeId}' date '${businessDate}' counter '${posCounter}'`,
      );
    }

    const currentPayload = { ...(session.payload ?? {}) } as Record<string, unknown>;
    if (currentPayload.cashHandOverPrintedAtUtc) return;

    const mergedPayload: Record<string, unknown> = {
      ...currentPayload,
      cashHandOverPrintedAtUtc: printedAt,
      cashHandOverPrintedEventId: meta.eventId,
    };

    await this.dayCloseModel.updateOne(
      { storeId: meta.storeId, businessDate, posCounter },
      { $set: { payload: mergedPayload } },
    );
  }

  async applyPaymentRecorded(meta: StoreSyncEventMeta, payload: Record<string, unknown>): Promise<void> {
    const existing = await this.gatewayPaymentModel.findOne({ sourceEventId: meta.eventId }).lean();
    if (existing) return;

    const invoiceNo =
      this.optionalString(payload, 'invoiceNo') ?? this.optionalString(payload, 'billNo');
    if (!invoiceNo) throw new BadRequestException('invoiceNo is required');

    const amount = readNumber(payload.amount);
    if (!(amount > 0)) throw new BadRequestException('amount must be positive');

    try {
      await this.gatewayPaymentModel.create({
        storeId: meta.storeId,
        invoiceNo,
        sourceEventId: meta.eventId,
        deviceId: meta.deviceId,
        posCounter: this.optionalString(payload, 'posCounter'),
        payload: {
          ...payload,
          invoiceNo,
          amount,
          storeId: meta.storeId,
          deviceId: meta.deviceId,
          createdAt: this.optionalString(payload, 'createdAt') ?? new Date().toISOString(),
        },
      });
    } catch (err: unknown) {
      const dup =
        err && typeof err === 'object' && 'code' in err && (err as { code?: number }).code === 11000;
      if (dup) return;
      throw err;
    }
  }
}
