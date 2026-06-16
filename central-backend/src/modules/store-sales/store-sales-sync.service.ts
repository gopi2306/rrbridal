import { BadRequestException, ConflictException, Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { StoreDailyExpense, StoreDailyExpenseDocument } from './schemas/store-daily-expense.schema';
import { StoreAdjustment, StoreAdjustmentDocument } from './schemas/store-adjustment.schema';
import { StoreCreditNoteCashout, StoreCreditNoteCashoutDocument } from './schemas/store-credit-note-cashout.schema';
import { StoreCreditNote, StoreCreditNoteDocument } from './schemas/store-credit-note.schema';
import { StoreInvoice, StoreInvoiceDocument } from './schemas/store-invoice.schema';
import { StoreSaleReturn, StoreSaleReturnDocument } from './schemas/store-sale-return.schema';

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
    const duplicate = await this.creditNoteModel
      .findOne({ storeId: meta.storeId, creditNoteNo })
      .lean();
    if (duplicate) return;

    const amount = this.requireNumber(payload, 'amount');
    const remaining =
      payload.remainingAmount !== undefined
        ? this.requireNumber(payload, 'remainingAmount')
        : amount;

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

    const expenseNo = this.requireString(payload, 'expenseNo');
    const description = this.requireString(payload, 'description');
    const businessDate = this.requireString(payload, 'businessDate');
    if (!/^\d{4}-\d{2}-\d{2}$/.test(businessDate)) {
      throw new BadRequestException('businessDate must be YYYY-MM-DD');
    }

    const amount = this.requireNumber(payload, 'amount');
    if (amount <= 0) throw new BadRequestException('amount must be positive');

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
        payload: {
          ...payload,
          expenseNo,
          description,
          businessDate,
          amount,
          status: this.optionalString(payload, 'status') ?? 'posted',
        },
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
}
