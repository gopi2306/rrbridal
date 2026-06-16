import { BadRequestException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import {
  buildExportFilename,
  buildMultiSheetExcelBuffer,
  type TabularExportSheet,
} from '../../common/tabular-export';
import { roundMoney } from '../../common/money.util';
import { StoreAdjustment, StoreAdjustmentDocument } from '../store-sales/schemas/store-adjustment.schema';
import { StoreCashMovement, StoreCashMovementDocument } from '../store-sales/schemas/store-cash-movement.schema';
import { StoreCreditNoteCashout, StoreCreditNoteCashoutDocument } from '../store-sales/schemas/store-credit-note-cashout.schema';
import { StoreDailyExpense, StoreDailyExpenseDocument } from '../store-sales/schemas/store-daily-expense.schema';
import { StoreDayClose, StoreDayCloseDocument } from '../store-sales/schemas/store-day-close.schema';
import { StoreInvoice, StoreInvoiceDocument } from '../store-sales/schemas/store-invoice.schema';
import { StoreSaleReturn, StoreSaleReturnDocument } from '../store-sales/schemas/store-sale-return.schema';
import { Store, StoreDocument } from '../stores/schemas/store.schema';
import { StoreDayCloseDashboardService } from './store-day-close-dashboard.service';
import {
  buildCsvContent,
  buildDetailSections,
  buildMetadataRows,
  buildSummaryRows,
  formatMoney,
} from './store-day-close-report-sections';
import type {
  StoreDayCloseReportData,
  StoreDayCloseReportExportResult,
  StoreDayCloseReportOptions,
  StoreDayCloseReportSummary,
} from './store-day-close-report.types';
import {
  BUSINESS_TZ_IANA,
  buildStoreExpenseBusinessDateFilterForYmd,
  buildStoreSalePayloadTimeFilter,
  isInRange,
  parseInvoiceLines,
  parseOccurredAt,
  parsePaymentTotals,
  parseReturnCashRefund,
  parseReturnExchangePayments,
  readNumber,
  readString,
} from './store-sales-payload.util';
import type { ResolvedDateRange } from './store-sales-dashboard.types';

function readDocCreatedAt(doc: unknown): Date | undefined {
  if (!doc || typeof doc !== 'object') return undefined;
  const createdAt = (doc as { createdAt?: unknown }).createdAt;
  if (createdAt instanceof Date && !Number.isNaN(createdAt.getTime())) return createdAt;
  return undefined;
}

function parseYmdParts(ymd: string): { y: number; m: number; day: number } | null {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(ymd.trim());
  if (!m) return null;
  return { y: Number(m[1]), m: Number(m[2]) - 1, day: Number(m[3]) };
}

function businessDayBoundsUtc(y: number, m: number, day: number): { from: Date; to: Date } {
  const offsetMs = 5.5 * 60 * 60 * 1000;
  const from = new Date(Date.UTC(y, m, day, 0, 0, 0, 0) - offsetMs);
  const to = new Date(Date.UTC(y, m, day, 23, 59, 59, 999) - offsetMs);
  return { from, to };
}

function resolveSingleBusinessDay(ymd: string): ResolvedDateRange {
  const parts = parseYmdParts(ymd);
  if (!parts) throw new BadRequestException('businessDate must be YYYY-MM-DD');
  const bounds = businessDayBoundsUtc(parts.y, parts.m, parts.day);
  return {
    from: bounds.from,
    to: bounds.to,
    fromYmd: ymd,
    toYmd: ymd,
    label: ymd,
    bucketByMonth: false,
    timezone: BUSINESS_TZ_IANA,
  };
}

function formatCounter(posCounter?: string, deviceId?: string): string {
  const pos = posCounter?.trim() ?? '';
  const dev = deviceId?.trim() ?? '';
  if (pos && dev) return `POS${pos} · ${dev}`;
  if (pos) return `POS${pos}`;
  return dev;
}

function formatUtcLocal(iso?: string): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleString('en-IN', {
    timeZone: BUSINESS_TZ_IANA,
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).replace(',', '');
}

function extractCreditNoteRefs(payload: Record<string, unknown>): string {
  const payments = payload.payments;
  if (!Array.isArray(payments)) return '';
  const refs: string[] = [];
  for (const p of payments) {
    if (!p || typeof p !== 'object') continue;
    const row = p as Record<string, unknown>;
    const provider = readString(row.provider) ?? '';
    if (provider.toLowerCase() !== 'creditnote') continue;
    const ref = readString(row.reference);
    if (ref) refs.push(ref);
  }
  return refs.join('; ');
}

function matchesPosCounter(
  posCounter: string | undefined,
  filter: string | undefined,
): boolean {
  if (!filter?.trim()) return true;
  return (posCounter ?? '').trim().toLowerCase() === filter.trim().toLowerCase();
}

function sumInvoiceLineQty(payload: Record<string, unknown>): number {
  return parseInvoiceLines(payload).reduce((s, l) => s + l.qty, 0);
}

@Injectable()
export class StoreDayCloseReportService {
  constructor(
    @InjectModel(Store.name) private readonly storeModel: Model<StoreDocument>,
    @InjectModel(StoreDayClose.name) private readonly dayCloseModel: Model<StoreDayCloseDocument>,
    @InjectModel(StoreInvoice.name) private readonly invoiceModel: Model<StoreInvoiceDocument>,
    @InjectModel(StoreSaleReturn.name) private readonly returnModel: Model<StoreSaleReturnDocument>,
    @InjectModel(StoreAdjustment.name) private readonly adjustmentModel: Model<StoreAdjustmentDocument>,
    @InjectModel(StoreDailyExpense.name) private readonly dailyExpenseModel: Model<StoreDailyExpenseDocument>,
    @InjectModel(StoreCashMovement.name) private readonly cashMovementModel: Model<StoreCashMovementDocument>,
    @InjectModel(StoreCreditNoteCashout.name)
    private readonly creditNoteCashoutModel: Model<StoreCreditNoteCashoutDocument>,
    private readonly dayCloseDashboardService: StoreDayCloseDashboardService,
  ) {}

  async buildExport(options: StoreDayCloseReportOptions): Promise<StoreDayCloseReportExportResult> {
    const data = await this.loadReportData(options);
    const scope = options.posCounter?.trim()
      ? `${options.businessDate}-pos${options.posCounter.trim()}`
      : `${options.businessDate}-all`;
    const filename = buildExportFilename('day-close', `${data.store.code}-${scope}`, options.format);

    if (options.format === 'csv') {
      const content = buildCsvContent(data);
      return {
        buffer: Buffer.from(content, 'utf8'),
        contentType: 'text/csv; charset=utf-8',
        filename,
      };
    }

    const sheets: TabularExportSheet[] = [
      {
        name: 'METADATA',
        headers: ['Label', 'Value'],
        rows: buildMetadataRows(data).map((r) => [r.label, r.value]),
      },
      {
        name: 'SUMMARY',
        headers: ['Label', 'Value'],
        rows: buildSummaryRows(data).map((r) => [r.label, r.value]),
      },
      ...buildDetailSections(data).map((s) => ({
        name: s.name.slice(0, 31),
        headers: s.headers,
        rows: s.rows,
      })),
    ];

    return {
      buffer: buildMultiSheetExcelBuffer(sheets),
      contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      filename,
    };
  }

  private async loadReportData(options: StoreDayCloseReportOptions): Promise<StoreDayCloseReportData> {
    const store = await this.resolveStore(options.storeId);
    const businessDate = options.businessDate;
    const range = resolveSingleBusinessDay(businessDate);
    const posFilter = options.posCounter?.trim();

    const dashboard = await this.dayCloseDashboardService.getDayCloseDashboard({
      storeId: store.code,
      businessDate,
      ...(posFilter ? { posCounter: posFilter } : {}),
    });

    const timeFilter = buildStoreSalePayloadTimeFilter(store.code, range);
    const [invoices, returns, adjustments, expenses, movements, cashouts] = await Promise.all([
      this.invoiceModel.find(timeFilter).lean(),
      this.returnModel.find({ storeId: store.code }).lean(),
      this.adjustmentModel.find({ storeId: store.code }).lean(),
      this.dailyExpenseModel.find(buildStoreExpenseBusinessDateFilterForYmd(store.code, businessDate, businessDate)).lean(),
      this.cashMovementModel.find({
        storeId: store.code,
        'payload.businessDate': businessDate,
      }).lean(),
      this.creditNoteCashoutModel.find({ storeId: store.code }).lean(),
    ]);

    const dayReturns = returns.filter((doc) => {
      const payload = (doc.payload ?? {}) as Record<string, unknown>;
      const status = readString(payload.status) ?? 'posted';
      if (status !== 'posted') return false;
      const occurred = parseOccurredAt(payload, readDocCreatedAt(doc));
      if (!occurred || !isInRange(occurred, range)) return false;
      const pos = readString(payload.posCounter) ?? readString((doc as { posCounter?: string }).posCounter);
      return matchesPosCounter(pos, posFilter);
    });

    const dayAdjustments = adjustments.filter((doc) => {
      const payload = (doc.payload ?? {}) as Record<string, unknown>;
      const status = readString(payload.status) ?? 'posted';
      if (status !== 'posted') return false;
      const occurred = parseOccurredAt(payload, readDocCreatedAt(doc));
      if (!occurred || !isInRange(occurred, range)) return false;
      const pos = readString(payload.posCounter);
      return matchesPosCounter(pos, posFilter);
    });

    const dayInvoices = invoices.filter((doc) => {
      const payload = (doc.payload ?? {}) as Record<string, unknown>;
      const status = readString(payload.status) ?? 'posted';
      if (status !== 'posted') return false;
      const pos = readString(payload.posCounter) ?? doc.posCounter;
      return matchesPosCounter(pos, posFilter);
    });

    const dayExpenses = expenses.filter((doc) => {
      const payload = (doc.payload ?? {}) as Record<string, unknown>;
      const status = readString(payload.status) ?? 'posted';
      if (status === 'void' || status === 'cancelled') return false;
      const pos = readString(payload.posCounter);
      return matchesPosCounter(pos, posFilter);
    });

    const dayMovements = movements.filter((doc) => {
      const payload = (doc.payload ?? {}) as Record<string, unknown>;
      const status = readString(payload.status) ?? 'posted';
      if (status !== 'posted') return false;
      const pos = readString(payload.posCounter);
      return matchesPosCounter(pos, posFilter);
    });

    const dayCashouts = cashouts.filter((doc) => {
      if (doc.status !== 'posted') return false;
      const occurred = doc.createdAtUtc ? new Date(doc.createdAtUtc) : readDocCreatedAt(doc);
      if (!occurred || Number.isNaN(occurred.getTime()) || !isInRange(occurred, range)) return false;
      return matchesPosCounter(doc.posCounter, posFilter);
    });

    const returnNosByBill = new Map<string, string>();
    for (const doc of dayReturns) {
      const payload = (doc.payload ?? {}) as Record<string, unknown>;
      const billNo = readString(payload.originalBillNo) ?? '';
      if (billNo) returnNosByBill.set(billNo, doc.returnNo);
    }

    const adjustmentNosByBill = new Map<string, string>();
    for (const doc of dayAdjustments) {
      const payload = (doc.payload ?? {}) as Record<string, unknown>;
      const billNo = readString(payload.originalBillNo) ?? '';
      if (billNo) adjustmentNosByBill.set(billNo, doc.adjustmentNo);
    }

    let cashTotal = 0;
    let cardTotal = 0;
    let upiTotal = 0;
    let creditNoteTotal = 0;
    let returnCashRefundTotal = 0;
    let creditNoteIssuedTotal = 0;
    let returnTotalAmount = 0;
    let exchangeCash = 0;
    let exchangeCard = 0;
    let exchangeUpi = 0;

    for (const doc of dayReturns) {
      const payload = (doc.payload ?? {}) as Record<string, unknown>;
      returnTotalAmount += readNumber(payload.returnTotal);
      returnCashRefundTotal += parseReturnCashRefund(payload);
      const mode = readString(payload.returnMode) ?? '';
      if (mode === 'credit_note') {
        creditNoteIssuedTotal += readNumber(payload.creditBalance);
      }
      const exchange = parseReturnExchangePayments(payload);
      exchangeCash += exchange.cash;
      exchangeCard += exchange.card;
      exchangeUpi += exchange.upi;
    }

    const creditNoteCashoutTotal = roundMoney(
      dayCashouts.reduce((s, d) => s + readNumber(d.cashRefunded), 0),
    );
    const cashRefundTotal = roundMoney(returnCashRefundTotal + creditNoteCashoutTotal);

    let dailyExpensesTotal = 0;
    for (const doc of dayExpenses) {
      dailyExpensesTotal += readNumber((doc.payload as Record<string, unknown>).amount);
    }
    dailyExpensesTotal = roundMoney(dailyExpensesTotal);

    let depositsTotal = 0;
    let withdrawalsTotal = 0;
    for (const doc of dayMovements) {
      const payload = (doc.payload ?? {}) as Record<string, unknown>;
      const amount = readNumber(payload.amount);
      const type = readString(payload.movementType) ?? '';
      if (type === 'deposit_to_bank') depositsTotal += amount;
      else if (type === 'cash_withdrawal') withdrawalsTotal += amount;
    }
    depositsTotal = roundMoney(depositsTotal);
    withdrawalsTotal = roundMoney(withdrawalsTotal);

    for (const doc of dayInvoices) {
      const payload = (doc.payload ?? {}) as Record<string, unknown>;
      const payments = parsePaymentTotals(payload);
      cashTotal += payments.cash;
      cardTotal += payments.card;
      upiTotal += payments.upi;
      creditNoteTotal += payments.creditNote;
    }
    cashTotal = roundMoney(cashTotal);
    cardTotal = roundMoney(cardTotal);
    upiTotal = roundMoney(upiTotal);
    creditNoteTotal = roundMoney(creditNoteTotal);

    const openingCash = roundMoney(dashboard.totals.openingCash);
    const netCashInHand = roundMoney(cashTotal - cashRefundTotal + exchangeCash - dailyExpensesTotal);
    const netCardInHand = roundMoney(cardTotal + exchangeCard);
    const netUpiInHand = roundMoney(upiTotal + exchangeUpi);
    const expectedCash = roundMoney(openingCash + netCashInHand - depositsTotal + withdrawalsTotal);
    const actualCashCounted = roundMoney(dashboard.totals.actualCashCounted);
    const cashDifference = roundMoney(dashboard.totals.cashDifference);

    const summary: StoreDayCloseReportSummary = {
      openingCash,
      cashTotal,
      returnCashRefundTotal,
      creditNoteCashoutTotal,
      dailyExpensesTotal,
      depositsTotal,
      withdrawalsTotal,
      expectedCash,
      actualCashCounted,
      cashDifference,
      netCashInHand,
      netCardInHand,
      netUpiInHand,
      actualHandInTotal: roundMoney(netCashInHand + netCardInHand + netUpiInHand),
      billCount: dayInvoices.length,
      returnCount: dayReturns.length,
      cardTotal,
      upiTotal,
      creditNoteTotal,
      returnTotalAmount: roundMoney(returnTotalAmount),
      creditNoteIssuedTotal: roundMoney(creditNoteIssuedTotal),
    };

    const sessionStatuses = dashboard.counters.map((c) => c.status);
    const sessionStatus = sessionStatuses.length === 0
      ? '—'
      : sessionStatuses.every((s) => s === 'closed')
        ? 'closed'
        : sessionStatuses.some((s) => s === 'open')
          ? 'open'
          : sessionStatuses[0] ?? '—';

    const dayCloseDocs = await this.dayCloseModel.find({
      storeId: store.code,
      businessDate,
      ...(posFilter ? { posCounter: posFilter } : {}),
    }).lean();

    const denominations: Array<Record<string, string>> = [];
    for (const doc of dayCloseDocs) {
      const payload = (doc.payload ?? {}) as Record<string, unknown>;
      if (readString(payload.status) !== 'closed') continue;
      const denoms = payload.cashDenominations;
      if (!Array.isArray(denoms)) continue;
      const counter = formatCounter(doc.posCounter, doc.deviceId);
      for (const line of denoms) {
        if (!line || typeof line !== 'object') continue;
        const row = line as Record<string, unknown>;
        const count = readNumber(row.unitCount);
        if (count <= 0) continue;
        const denomination = readNumber(row.denomination);
        denominations.push({
          counter,
          denomination: String(denomination),
          count: String(count),
          subtotal: formatMoney(denomination * count),
        });
      }
    }

    return {
      store: { code: store.code, name: store.name },
      businessDate,
      counterScope: posFilter ? `POS${posFilter}` : 'All counters',
      sessionStatus,
      exportedAt: formatUtcLocal(new Date().toISOString()),
      summary,
      counterRollup: dashboard.counters.map((c) => {
        const row = {
          posCounter: c.posCounter,
          status: c.status,
          openingCash: c.openingCash,
          expectedCash: c.expectedCash,
          actualCashCounted: c.actualCashCounted,
          cashDifference: c.cashDifference,
          closedAtLocal: formatUtcLocal(c.closedAtUtc),
        };
        return c.closedBy ? { ...row, closedBy: c.closedBy } : row;
      }),
      bills: dayInvoices.map((doc) => {
        const payload = (doc.payload ?? {}) as Record<string, unknown>;
        const billNo = readString(payload.billNo) ?? doc.invoiceNo;
        const payments = parsePaymentTotals(payload);
        const occurred = parseOccurredAt(payload, readDocCreatedAt(doc));
        return {
          billNo,
          postedAtLocal: occurred ? formatUtcLocal(occurred.toISOString()) : '—',
          billDate: readString(payload.billDate) ?? '',
          counter: formatCounter(readString(payload.posCounter) ?? doc.posCounter, doc.deviceId),
          customer: readString(payload.customerName) ?? '',
          mobile: readString(payload.customerPhone) ?? '',
          qty: String(sumInvoiceLineQty(payload)),
          payable: formatMoney(readNumber(payload.payable)),
          cash: formatMoney(payments.cash),
          card: formatMoney(payments.card),
          upi: formatMoney(payments.upi),
          creditNote: formatMoney(payments.creditNote),
          creditNoteRefs: extractCreditNoteRefs(payload),
          returned: returnNosByBill.has(billNo) ? 'Yes' : '—',
          returnNo: returnNosByBill.get(billNo) ?? '',
          adjustment: adjustmentNosByBill.has(billNo) ? 'Yes' : '—',
          adjustmentNo: adjustmentNosByBill.get(billNo) ?? '',
          sync: 'Synced',
        };
      }),
      returns: dayReturns.map((doc) => {
        const payload = (doc.payload ?? {}) as Record<string, unknown>;
        const occurred = parseOccurredAt(payload, readDocCreatedAt(doc));
        const exchange = parseReturnExchangePayments(payload);
        const parts: string[] = [];
        if (exchange.cash > 0) parts.push(`Cash ${formatMoney(exchange.cash)}`);
        if (exchange.card > 0) parts.push(`Card ${formatMoney(exchange.card)}`);
        if (exchange.upi > 0) parts.push(`UPI ${formatMoney(exchange.upi)}`);
        if (exchange.creditNote > 0) parts.push(`CN ${formatMoney(exchange.creditNote)}`);
        return {
          returnNo: doc.returnNo,
          originalBill: readString(payload.originalBillNo) ?? '',
          counter: formatCounter(readString(payload.posCounter), doc.deviceId),
          postedAtLocal: occurred ? formatUtcLocal(occurred.toISOString()) : '—',
          returnTotal: formatMoney(readNumber(payload.returnTotal)),
          mode: readString(payload.returnMode) ?? '',
          cashRefunded: formatMoney(readNumber(payload.cashRefunded)),
          creditBalance: formatMoney(readNumber(payload.creditBalance)),
          collected: formatMoney(readNumber(payload.amountCollected)),
          payments: parts.length > 0 ? parts.join(', ') : '—',
          creditNoteNo: readString(payload.creditNoteNo) ?? '',
        };
      }),
      adjustments: dayAdjustments.map((doc) => {
        const payload = (doc.payload ?? {}) as Record<string, unknown>;
        const occurred = parseOccurredAt(payload, readDocCreatedAt(doc));
        return {
          adjustmentNo: doc.adjustmentNo,
          originalBill: readString(payload.originalBillNo) ?? '',
          counter: formatCounter(readString(payload.posCounter), doc.deviceId),
          postedAtLocal: occurred ? formatUtcLocal(occurred.toISOString()) : '—',
          originalPayable: formatMoney(readNumber(payload.originalPayable)),
          adjustedPayable: formatMoney(readNumber(payload.adjustedPayable)),
          diffPayable: formatMoney(readNumber(payload.diffPayable)),
          reason: readString(payload.reason) ?? '',
        };
      }),
      expenses: dayExpenses.map((doc) => {
        const payload = (doc.payload ?? {}) as Record<string, unknown>;
        return {
          expenseNo: doc.expenseNo,
          counter: formatCounter(readString(payload.posCounter), doc.deviceId),
          businessDate: readString(payload.businessDate) ?? businessDate,
          description: readString(payload.description) ?? '',
          amount: formatMoney(readNumber(payload.amount)),
        };
      }),
      cashMovements: dayMovements.map((doc) => {
        const payload = (doc.payload ?? {}) as Record<string, unknown>;
        const type = readString(payload.movementType) ?? '';
        const typeDisplay = type === 'deposit_to_bank'
          ? 'Deposit to bank'
          : type === 'cash_withdrawal'
            ? 'Cash withdrawal'
            : type;
        return {
          movementNo: doc.movementNo,
          type: typeDisplay,
          counter: formatCounter(readString(payload.posCounter), doc.deviceId),
          amount: formatMoney(readNumber(payload.amount)),
          note: readString(payload.description) ?? '',
          postedAtLocal: formatUtcLocal(readString(payload.createdAtUtc)),
        };
      }),
      creditNoteCashouts: dayCashouts.map((doc) => ({
        cashoutNo: doc.cashoutNo,
        creditNoteNo: doc.creditNoteNo,
        amount: formatMoney(readNumber(doc.cashRefunded)),
        counter: doc.posCounter ? `POS${doc.posCounter}` : '—',
        postedAtLocal: formatUtcLocal(doc.createdAtUtc),
      })),
      denominations,
    };
  }

  private async resolveStore(storeId?: string) {
    const code = storeId?.trim().toLowerCase();
    const store = code
      ? await this.storeModel.findOne({ code, status: 'active' }).lean()
      : await this.storeModel.findOne({ status: 'active' }).sort({ code: 1 }).lean();

    if (!store) {
      if (code) throw new NotFoundException(`Store '${code}' not found or inactive`);
      throw new NotFoundException('No active stores configured');
    }

    return { code: store.code, name: store.name };
  }
}
