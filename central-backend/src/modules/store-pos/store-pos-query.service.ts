import { BadRequestException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { StoreAdjustment, StoreAdjustmentDocument } from '../store-sales/schemas/store-adjustment.schema';
import { StoreCashMovement, StoreCashMovementDocument } from '../store-sales/schemas/store-cash-movement.schema';
import { StoreCreditNote, StoreCreditNoteDocument } from '../store-sales/schemas/store-credit-note.schema';
import { StoreCreditNoteCashout, StoreCreditNoteCashoutDocument } from '../store-sales/schemas/store-credit-note-cashout.schema';
import { StoreDailyExpense, StoreDailyExpenseDocument } from '../store-sales/schemas/store-daily-expense.schema';
import { StoreDayClose, StoreDayCloseDocument } from '../store-sales/schemas/store-day-close.schema';
import { StoreGatewayPayment, StoreGatewayPaymentDocument } from '../store-sales/schemas/store-gateway-payment.schema';
import { StoreInvoice, StoreInvoiceDocument } from '../store-sales/schemas/store-invoice.schema';
import { StorePaymentReceipt, StorePaymentReceiptDocument } from '../store-sales/schemas/store-payment-receipt.schema';
import { StoreQuotation, StoreQuotationDocument } from '../store-sales/schemas/store-quotation.schema';
import { StoreSaleReturn, StoreSaleReturnDocument } from '../store-sales/schemas/store-sale-return.schema';
import { StoresService } from '../stores/stores.service';
import { PromotionSchemesService } from '../promotion-schemes/promotion-schemes.service';
import { StoreHeldBill, StoreHeldBillDocument } from './schemas/store-held-bill.schema';
import { StorePosCounter, StorePosCounterDocument } from './schemas/store-pos-counter.schema';

const NUMBER_KINDS: Record<string, string> = {
  billNo: '',
  holdNo: 'HOLD-',
  returnNo: 'RET-',
  adjustmentNo: 'ADJ-',
  expenseNo: 'EXP-',
  cashMovementNo: 'CMV-',
  quotationNo: 'QUOT-',
  paymentReceiptNo: 'RCPT-',
  creditNoteNo: 'CN-',
};

@Injectable()
export class StorePosQueryService {
  constructor(
    private readonly storesService: StoresService,
    private readonly promotionSchemesService: PromotionSchemesService,
    @InjectModel(StoreInvoice.name) private readonly invoiceModel: Model<StoreInvoiceDocument>,
    @InjectModel(StoreSaleReturn.name) private readonly returnModel: Model<StoreSaleReturnDocument>,
    @InjectModel(StoreQuotation.name) private readonly quotationModel: Model<StoreQuotationDocument>,
    @InjectModel(StoreCreditNote.name) private readonly creditNoteModel: Model<StoreCreditNoteDocument>,
    @InjectModel(StoreCreditNoteCashout.name)
    private readonly creditNoteCashoutModel: Model<StoreCreditNoteCashoutDocument>,
    @InjectModel(StoreDayClose.name) private readonly dayCloseModel: Model<StoreDayCloseDocument>,
    @InjectModel(StoreCashMovement.name) private readonly cashMovementModel: Model<StoreCashMovementDocument>,
    @InjectModel(StoreDailyExpense.name) private readonly dailyExpenseModel: Model<StoreDailyExpenseDocument>,
    @InjectModel(StoreAdjustment.name) private readonly adjustmentModel: Model<StoreAdjustmentDocument>,
    @InjectModel(StorePaymentReceipt.name)
    private readonly paymentReceiptModel: Model<StorePaymentReceiptDocument>,
    @InjectModel(StoreGatewayPayment.name)
    private readonly gatewayPaymentModel: Model<StoreGatewayPaymentDocument>,
    @InjectModel(StorePosCounter.name) private readonly counterModel: Model<StorePosCounterDocument>,
    @InjectModel(StoreHeldBill.name) private readonly heldBillModel: Model<StoreHeldBillDocument>,
  ) {}

  private async requireStore(storeCode: string) {
    const code = storeCode?.trim();
    if (!code) throw new BadRequestException('storeCode is required');
    const exists = await this.storesService.existsByCode(code);
    if (!exists) throw new BadRequestException(`Unknown storeCode '${code}'`);
    return code;
  }

  async listBills(storeCode: string, search?: string, limit = 50) {
    const storeId = await this.requireStore(storeCode);
    const take = Math.min(200, Math.max(1, limit));
    const filter: Record<string, unknown> = { storeId };
    if (search?.trim()) {
      const q = search.trim();
      filter.$or = [
        { invoiceNo: { $regex: q, $options: 'i' } },
        { 'payload.customerName': { $regex: q, $options: 'i' } },
        { 'payload.customerPhone': { $regex: q, $options: 'i' } },
      ];
    }
    const docs = await this.invoiceModel.find(filter).sort({ createdAt: -1 }).limit(take).lean();
    return docs.map((d) => {
      const row = d as Record<string, unknown>;
      return {
        billNo: d.invoiceNo,
        storeId: d.storeId,
        deviceId: d.deviceId,
        posCounter: d.posCounter ?? null,
        payload: d.payload ?? {},
        createdAt: row.createdAt ?? null,
      };
    });
  }

  async getBill(storeCode: string, billNo: string) {
    const storeId = await this.requireStore(storeCode);
    const no = billNo?.trim();
    if (!no) throw new BadRequestException('billNo is required');
    const doc = await this.invoiceModel.findOne({ storeId, invoiceNo: no }).lean();
    if (!doc) throw new NotFoundException(`Bill '${no}' not found`);
    const row = doc as Record<string, unknown>;
    return {
      billNo: doc.invoiceNo,
      storeId: doc.storeId,
      deviceId: doc.deviceId,
      posCounter: doc.posCounter ?? null,
      payload: doc.payload ?? {},
      createdAt: row.createdAt ?? null,
    };
  }

  async listReturns(storeCode: string, originalBillNo?: string, limit = 50) {
    const storeId = await this.requireStore(storeCode);
    const take = Math.min(200, Math.max(1, limit));
    const filter: Record<string, unknown> = { storeId };
    if (originalBillNo?.trim()) {
      filter['payload.originalBillNo'] = originalBillNo.trim();
    }
    const docs = await this.returnModel.find(filter).sort({ createdAt: -1 }).limit(take).lean();
    return docs.map((d) => {
      const row = d as Record<string, unknown>;
      return {
        returnNo: d.returnNo,
        kind: d.kind,
        storeId: d.storeId,
        payload: d.payload ?? {},
        createdAt: row.createdAt ?? null,
      };
    });
  }

  async getReturn(storeCode: string, returnNo: string) {
    const storeId = await this.requireStore(storeCode);
    const no = returnNo?.trim();
    if (!no) throw new BadRequestException('returnNo is required');
    const doc = await this.returnModel.findOne({ storeId, returnNo: no }).lean();
    if (!doc) throw new NotFoundException(`Return '${no}' not found`);
    const row = doc as Record<string, unknown>;
    return {
      returnNo: doc.returnNo,
      kind: doc.kind,
      storeId: doc.storeId,
      payload: doc.payload ?? {},
      createdAt: row.createdAt ?? null,
    };
  }

  async listQuotations(storeCode: string, status?: string, limit = 50) {
    const storeId = await this.requireStore(storeCode);
    const take = Math.min(200, Math.max(1, limit));
    const filter: Record<string, unknown> = { storeId };
    if (status?.trim()) filter.status = status.trim();
    const docs = await this.quotationModel.find(filter).sort({ updatedAt: -1 }).limit(take).lean();
    return docs.map((d) => {
      const row = d as Record<string, unknown>;
      return {
        quotationNo: d.quotationNo,
        status: d.status,
        convertedBillNo: d.convertedBillNo ?? null,
        storeId: d.storeId,
        payload: d.payload ?? {},
        createdAt: row.createdAt ?? null,
        updatedAt: row.updatedAt ?? null,
      };
    });
  }

  async getQuotation(storeCode: string, quotationNo: string) {
    const storeId = await this.requireStore(storeCode);
    const no = quotationNo?.trim();
    if (!no) throw new BadRequestException('quotationNo is required');
    const doc = await this.quotationModel.findOne({ storeId, quotationNo: no }).lean();
    if (!doc) throw new NotFoundException(`Quotation '${no}' not found`);
    const row = doc as Record<string, unknown>;
    return {
      quotationNo: doc.quotationNo,
      status: doc.status,
      convertedBillNo: doc.convertedBillNo ?? null,
      storeId: doc.storeId,
      payload: doc.payload ?? {},
      createdAt: row.createdAt ?? null,
      updatedAt: row.updatedAt ?? null,
    };
  }

  async listCreditNotes(storeCode: string, customerPhone?: string, customerCode?: string, availableOnly = false) {
    const storeId = await this.requireStore(storeCode);
    const filter: Record<string, unknown> = { storeId };
    if (availableOnly) filter.status = 'available';
    if (customerPhone?.trim()) filter.customerPhone = customerPhone.trim();
    if (customerCode?.trim()) filter.customerCode = customerCode.trim();
    const docs = await this.creditNoteModel.find(filter).sort({ updatedAt: -1 }).limit(100).lean();
    return docs;
  }

  async getCreditNote(storeCode: string, creditNoteNo: string) {
    const storeId = await this.requireStore(storeCode);
    const no = creditNoteNo?.trim();
    if (!no) throw new BadRequestException('creditNoteNo is required');
    const doc = await this.creditNoteModel.findOne({ storeId, creditNoteNo: no }).lean();
    if (!doc) throw new NotFoundException(`Credit note '${no}' not found`);
    return doc;
  }

  async getDaySession(storeCode: string, businessDate: string, posCounter: string) {
    const storeId = await this.requireStore(storeCode);
    const date = businessDate?.trim();
    const counter = posCounter?.trim();
    if (!date || !counter) throw new BadRequestException('businessDate and posCounter are required');
    const doc = await this.dayCloseModel.findOne({ storeId, businessDate: date, posCounter: counter }).lean();
    if (!doc) return null;
    const row = doc as Record<string, unknown>;
    return {
      storeId: doc.storeId,
      businessDate: doc.businessDate,
      posCounter: doc.posCounter,
      payload: doc.payload ?? {},
      createdAt: row.createdAt ?? null,
      updatedAt: row.updatedAt ?? null,
    };
  }

  async listCashMovements(storeCode: string, businessDate: string, limit = 100) {
    const storeId = await this.requireStore(storeCode);
    const date = businessDate?.trim();
    if (!date) throw new BadRequestException('businessDate is required');
    const take = Math.min(200, Math.max(1, limit));
    const docs = await this.cashMovementModel
      .find({ storeId, 'payload.businessDate': date })
      .sort({ createdAt: -1 })
      .limit(take)
      .lean();
    return docs.map((d) => {
      const row = d as Record<string, unknown>;
      return {
        movementNo: d.movementNo,
        storeId: d.storeId,
        payload: d.payload ?? {},
        createdAt: row.createdAt ?? null,
      };
    });
  }

  async listDailyExpenses(storeCode: string, businessDate: string, limit = 100) {
    const storeId = await this.requireStore(storeCode);
    const date = businessDate?.trim();
    if (!date) throw new BadRequestException('businessDate is required');
    const take = Math.min(200, Math.max(1, limit));
    const docs = await this.dailyExpenseModel
      .find({ storeId, 'payload.businessDate': date })
      .sort({ createdAt: -1 })
      .limit(take)
      .lean();
    return docs.map((d) => {
      const row = d as Record<string, unknown>;
      return {
        expenseNo: d.expenseNo,
        storeId: d.storeId,
        payload: d.payload ?? {},
        createdAt: row.createdAt ?? null,
      };
    });
  }

  async nextNumber(input: {
    storeId: string;
    deviceId: string;
    posCounter: string;
    kind: string;
  }) {
    const storeId = input.storeId?.trim();
    const deviceId = input.deviceId?.trim();
    const posCounter = input.posCounter?.trim() || '1';
    const kind = input.kind?.trim();
    if (!storeId || !deviceId || !kind) {
      throw new BadRequestException('storeId, deviceId, and kind are required');
    }
    if (!(kind in NUMBER_KINDS)) {
      throw new BadRequestException(`Unsupported number kind '${kind}'`);
    }

    await this.requireStore(storeId);
    const counterKey = `${kind}:${storeId}:${deviceId}`;
    const updated = await this.counterModel.findOneAndUpdate(
      { key: counterKey },
      { $inc: { seq: 1 }, $setOnInsert: { key: counterKey } },
      { upsert: true, new: true },
    );
    const seq = updated?.seq ?? 1;
    const date = new Date();
    const ymd =
      `${date.getFullYear()}${String(date.getMonth() + 1).padStart(2, '0')}${String(date.getDate()).padStart(2, '0')}`;
    const storeSuffix = storeId.slice(-3).toUpperCase().padStart(3, '0');
    const prefix = NUMBER_KINDS[kind] ?? '';
    const value = `${prefix}${ymd}-${storeSuffix}-${posCounter}-${String(seq).padStart(4, '0')}`;
    return { kind, value, seq };
  }

  async listHeldBills(storeId: string, deviceId?: string) {
    const code = await this.requireStore(storeId);
    const filter: Record<string, unknown> = { storeId: code };
    if (deviceId?.trim()) filter.deviceId = deviceId.trim();
    return await this.heldBillModel.find(filter).sort({ updatedAt: -1 }).limit(100).lean();
  }

  async upsertHeldBill(storeId: string, deviceId: string, holdNo: string, payload: Record<string, unknown>) {
    const code = await this.requireStore(storeId);
    const no = holdNo?.trim();
    const device = deviceId?.trim();
    if (!no || !device) throw new BadRequestException('holdNo and deviceId are required');
    await this.heldBillModel.findOneAndUpdate(
      { storeId: code, holdNo: no },
      { storeId: code, deviceId: device, holdNo: no, payload },
      { upsert: true, new: true },
    );
    return { storeId: code, holdNo: no, ok: true };
  }

  async deleteHeldBill(storeId: string, holdNo: string) {
    const code = await this.requireStore(storeId);
    const no = holdNo?.trim();
    if (!no) throw new BadRequestException('holdNo is required');
    await this.heldBillModel.deleteOne({ storeId: code, holdNo: no });
    return { storeId: code, holdNo: no, ok: true };
  }

  async getPaymentReceipt(storeCode: string, receiptNo: string) {
    const storeId = await this.requireStore(storeCode);
    const no = receiptNo?.trim();
    if (!no) throw new BadRequestException('receiptNo is required');
    const doc = await this.paymentReceiptModel.findOne({ storeId, receiptNo: no }).lean();
    if (!doc) throw new NotFoundException(`Payment receipt '${no}' not found`);
    const row = doc as Record<string, unknown>;
    return {
      receiptNo: doc.receiptNo,
      billNo: doc.billNo,
      storeId: doc.storeId,
      deviceId: doc.deviceId,
      payload: doc.payload ?? {},
      createdAt: row.createdAt ?? null,
    };
  }

  async listAdjustments(storeCode: string, originalBillNo?: string, limit = 50) {
    const storeId = await this.requireStore(storeCode);
    const take = Math.min(200, Math.max(1, limit));
    const filter: Record<string, unknown> = { storeId };
    if (originalBillNo?.trim()) {
      filter['payload.originalBillNo'] = originalBillNo.trim();
    }
    const docs = await this.adjustmentModel.find(filter).sort({ createdAt: -1 }).limit(take).lean();
    return docs.map((d) => {
      const row = d as Record<string, unknown>;
      return {
        adjustmentNo: d.adjustmentNo,
        storeId: d.storeId,
        deviceId: d.deviceId,
        payload: d.payload ?? {},
        createdAt: row.createdAt ?? null,
      };
    });
  }

  async getAdjustmentByOriginalBill(storeCode: string, originalBillNo: string) {
    const storeId = await this.requireStore(storeCode);
    const billNo = originalBillNo?.trim();
    if (!billNo) throw new BadRequestException('originalBillNo is required');
    const doc = await this.adjustmentModel
      .findOne({
        storeId,
        'payload.originalBillNo': billNo,
        $or: [{ 'payload.status': 'posted' }, { 'payload.status': { $exists: false } }],
      })
      .sort({ createdAt: -1 })
      .lean();
    if (!doc) return null;
    const row = doc as Record<string, unknown>;
    return {
      adjustmentNo: doc.adjustmentNo,
      storeId: doc.storeId,
      deviceId: doc.deviceId,
      payload: doc.payload ?? {},
      createdAt: row.createdAt ?? null,
    };
  }

  async listGatewayPayments(storeCode: string, limit = 100, posCounter?: string) {
    const storeId = await this.requireStore(storeCode);
    const take = Math.min(500, Math.max(1, limit));
    const filter: Record<string, unknown> = { storeId };
    if (posCounter?.trim()) filter.posCounter = posCounter.trim();
    const docs = await this.gatewayPaymentModel.find(filter).sort({ createdAt: -1 }).limit(take).lean();
    return docs.map((d) => {
      const row = d as Record<string, unknown>;
      return {
        invoiceNo: d.invoiceNo,
        storeId: d.storeId,
        deviceId: d.deviceId,
        posCounter: d.posCounter ?? null,
        payload: d.payload ?? {},
        createdAt: row.createdAt ?? null,
      };
    });
  }

  async listCreditNoteCashouts(storeCode: string, businessDate?: string, limit = 100) {
    const storeId = await this.requireStore(storeCode);
    const take = Math.min(200, Math.max(1, limit));
    const filter: Record<string, unknown> = { storeId };
    if (businessDate?.trim()) {
      const day = businessDate.trim();
      const start = new Date(`${day}T00:00:00.000Z`);
      const end = new Date(`${day}T23:59:59.999Z`);
      filter.createdAtUtc = { $gte: start.toISOString(), $lte: end.toISOString() };
    }
    const docs = await this.creditNoteCashoutModel.find(filter).sort({ createdAtUtc: -1 }).limit(take).lean();
    return docs.map((d) => ({
      cashoutNo: d.cashoutNo,
      creditNoteNo: d.creditNoteNo,
      storeId: d.storeId,
      posCounter: d.posCounter ?? null,
      cashRefunded: d.cashRefunded,
      createdAtUtc: d.createdAtUtc,
      status: d.status,
    }));
  }

  async listActivePromotions(storeCode: string) {
    const storeId = await this.requireStore(storeCode);
    return await this.promotionSchemesService.listActiveForStore(storeId);
  }
}
