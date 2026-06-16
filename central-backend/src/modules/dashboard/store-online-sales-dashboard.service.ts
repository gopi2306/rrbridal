import { BadRequestException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { roundMoney } from '../../common/money.util';
import { StoreInvoice, StoreInvoiceDocument } from '../store-sales/schemas/store-invoice.schema';
import { Store, StoreDocument } from '../stores/schemas/store.schema';
import {
  buildDashboardPeriod,
  buildStoreSalePayloadTimeFilter,
  isOnlineCodBill,
  isOnlineCodPending,
  parseOccurredAt,
  parseOnlineCodAmount,
  parseOnlineCodReceivedPaymentMode,
  parseOnlineCodTransactionNo,
  readOnlineCodStatus,
  readString,
  resolveDateRange,
} from './store-sales-payload.util';
import type {
  StoreOnlineSalesDashboardOptions,
  StoreOnlineSalesDashboardResponse,
  StoreOnlineSalesItemRow,
  StoreOnlineSalesSummary,
} from './store-online-sales-dashboard.types';

@Injectable()
export class StoreOnlineSalesDashboardService {
  constructor(
    @InjectModel(StoreInvoice.name) private readonly invoiceModel: Model<StoreInvoiceDocument>,
    @InjectModel(Store.name) private readonly storeModel: Model<StoreDocument>,
  ) {}

  async getOnlineSalesDashboard(
    options: StoreOnlineSalesDashboardOptions,
  ): Promise<StoreOnlineSalesDashboardResponse> {
    const store = await this.resolveStore(options.storeId);
    let range;
    try {
      range = resolveDateRange({
        period: options.period,
        ...(options.from !== undefined ? { from: options.from } : {}),
        ...(options.to !== undefined ? { to: options.to } : {}),
        year: options.year,
        month: options.month,
      });
    } catch (err: unknown) {
      throw new BadRequestException(err instanceof Error ? err.message : String(err));
    }

    const invoices = await this.invoiceModel
      .find(buildStoreSalePayloadTimeFilter(store.code, range))
      .lean();

    const onlineInvoices = invoices.filter((inv) => isOnlineCodBill(inv.payload as Record<string, unknown>));

    const allOnline = await this.invoiceModel.find({ storeId: store.code }).lean();
    const pendingAll = allOnline.filter((inv) =>
      isOnlineCodPending(inv.payload as Record<string, unknown>),
    );

    let balanceTill = 0;
    let pendingCount = 0;
    for (const inv of pendingAll) {
      const payload = inv.payload as Record<string, unknown>;
      pendingCount++;
      balanceTill += parseOnlineCodAmount(payload);
    }

    const statusFilter = options.status ?? 'all';
    const items: StoreOnlineSalesItemRow[] = [];
    let receivedCount = 0;
    let receivedAmount = 0;

    for (const inv of onlineInvoices) {
      const payload = inv.payload as Record<string, unknown>;
      const status = readOnlineCodStatus(payload) || 'pending';
      if (statusFilter !== 'all' && status.toLowerCase() !== statusFilter) continue;

      const amount = parseOnlineCodAmount(payload);
      if (status.toLowerCase() === 'received') {
        receivedCount++;
        receivedAmount += amount;
      }

      const occurred = parseOccurredAt(payload, this.docTimestamp(inv));
      const row: StoreOnlineSalesItemRow = {
        billNo: inv.invoiceNo,
        customerName: readString(payload.customerName) ?? '',
        customerPhone: readString(payload.customerPhone) ?? '',
        amount: roundMoney(amount),
        status,
        occurredAt: occurred?.toISOString() ?? '',
      };
      const txn = parseOnlineCodTransactionNo(payload);
      if (txn) row.transactionNo = txn;
      const mode = parseOnlineCodReceivedPaymentMode(payload);
      if (mode) row.receivedPaymentMode = mode;
      items.push(row);
    }

    items.sort((a, b) => b.occurredAt.localeCompare(a.occurredAt));

    const summary: StoreOnlineSalesSummary = {
      balanceTill: roundMoney(balanceTill),
      pendingCount,
      pendingAmount: roundMoney(balanceTill),
      receivedCount,
      receivedAmount: roundMoney(receivedAmount),
      totalOnlineOrders: onlineInvoices.length,
    };

    return {
      store: { code: store.code, name: store.name },
      period: buildDashboardPeriod(options.period, range),
      summary,
      items,
    };
  }

  private async resolveStore(storeId?: string) {
    const code = storeId?.trim().toLowerCase();
    const store = code
      ? await this.storeModel.findOne({ code, status: 'active' }).lean()
      : await this.storeModel.findOne({ status: 'active' }).sort({ code: 1 }).lean();

    if (!store) {
      if (code) throw new NotFoundException(`Store '${code}' not found or inactive`);
      throw new NotFoundException('No active store found');
    }

    return { code: store.code, name: store.name };
  }

  private docTimestamp(doc: Record<string, unknown>): unknown {
    return doc.updatedAt ?? doc.createdAt;
  }
}
