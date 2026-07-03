import { Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { roundMoney } from '../../common/money.util';
import { StoreDayClose, StoreDayCloseDocument } from '../store-sales/schemas/store-day-close.schema';
import { Store, StoreDocument } from '../stores/schemas/store.schema';
import type {
  StoreDayCloseCounterRow,
  StoreDayCloseDashboardOptions,
  StoreDayCloseDashboardResponse,
} from './store-day-close-dashboard.types';

@Injectable()
export class StoreDayCloseDashboardService {
  constructor(
    @InjectModel(StoreDayClose.name) private readonly dayCloseModel: Model<StoreDayCloseDocument>,
    @InjectModel(Store.name) private readonly storeModel: Model<StoreDocument>,
  ) {}

  async getDayCloseDashboard(options: StoreDayCloseDashboardOptions): Promise<StoreDayCloseDashboardResponse> {
    const store = await this.resolveStore(options.storeId);
    const storeId = store.code;

    const filter: Record<string, unknown> = {
      storeId,
      businessDate: options.businessDate,
    };
    if (options.posCounter) filter.posCounter = options.posCounter;

    const docs = await this.dayCloseModel.find(filter).sort({ posCounter: 1 }).lean();
    const counters: StoreDayCloseCounterRow[] = docs.map((doc) => {
      const payload = (doc.payload ?? {}) as Record<string, unknown>;
      const row: StoreDayCloseCounterRow = {
        posCounter: doc.posCounter,
        status: String(payload.status ?? 'open'),
        openingCash: roundMoney(Number(payload.openingCash ?? 0)),
        expectedCash: roundMoney(Number(payload.expectedCash ?? 0)),
        actualCashCounted: roundMoney(Number(payload.actualCashCounted ?? 0)),
        cashDifference: roundMoney(Number(payload.cashDifference ?? 0)),
        payload,
      };
      if (payload.closedBy) row.closedBy = String(payload.closedBy);
      if (payload.closedAtUtc) row.closedAtUtc = String(payload.closedAtUtc);
      return row;
    });

    return {
      storeId,
      date: options.businessDate,
      businessDate: options.businessDate,
      counters,
      totals: {
        openingCash: roundMoney(counters.reduce((s, c) => s + c.openingCash, 0)),
        expectedCash: roundMoney(counters.reduce((s, c) => s + c.expectedCash, 0)),
        actualCashCounted: roundMoney(counters.reduce((s, c) => s + c.actualCashCounted, 0)),
        cashDifference: roundMoney(counters.reduce((s, c) => s + c.cashDifference, 0)),
      },
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
