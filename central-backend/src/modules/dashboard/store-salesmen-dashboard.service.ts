import { BadRequestException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { roundMoney } from '../../common/money.util';
import { StoreInvoice, StoreInvoiceDocument } from '../store-sales/schemas/store-invoice.schema';
import { Store, StoreDocument } from '../stores/schemas/store.schema';
import { resolveDashboardStore } from './dashboard-store.util';
import {
  buildDashboardPeriod,
  buildStoreSalePayloadTimeFilter,
  parseInvoiceNet,
  parseOccurredAt,
  readString,
  resolveDateRange,
  sumInvoiceLineQty,
} from './store-sales-payload.util';
import {
  LEGACY_SALESMAN_ID,
  type StoreSalesmanDashboardOptions,
  type StoreSalesmanDashboardResponse,
  type StoreSalesmenDashboardOptions,
  type StoreSalesmenDashboardResponse,
  type StoreSalesmenInvoiceRow,
  type StoreSalesmenSalesmanRow,
} from './store-salesmen-dashboard.types';

type SalesmanGroup = {
  salesmanId: string;
  salesmanCode: string | null;
  salesmanName: string;
  invoices: number;
  itemsSold: number;
  totalBillAmount: number;
};

type InvoiceLean = {
  invoiceNo: string;
  createdAt?: Date;
  payload?: Record<string, unknown>;
};

@Injectable()
export class StoreSalesmenDashboardService {
  constructor(
    @InjectModel(StoreInvoice.name) private readonly invoiceModel: Model<StoreInvoiceDocument>,
    @InjectModel(Store.name) private readonly storeModel: Model<StoreDocument>,
  ) {}

  async getAllSalesmenDashboard(
    options: StoreSalesmenDashboardOptions,
  ): Promise<StoreSalesmenDashboardResponse> {
    const store = await resolveDashboardStore(this.storeModel, options.storeId);
    const range = this.resolveRange(options);
    const invoices = await this.loadInvoices(store.code, range);
    const groups = this.aggregateSalesmen(invoices);
    const salesmen = [...groups.values()].sort((a, b) => b.totalBillAmount - a.totalBillAmount);

    const summary = {
      salesmenCount: salesmen.length,
      invoices: salesmen.reduce((s, r) => s + r.invoices, 0),
      itemsSold: roundMoney(salesmen.reduce((s, r) => s + r.itemsSold, 0)),
      totalBillAmount: roundMoney(salesmen.reduce((s, r) => s + r.totalBillAmount, 0)),
    };

    return {
      store: { code: store.code, name: store.name },
      period: buildDashboardPeriod(options.period, range),
      summary,
      salesmen,
      recentInvoices: this.buildRecentInvoices(invoices, options.invoiceLimit),
    };
  }

  async getSingleSalesmanDashboard(
    options: StoreSalesmanDashboardOptions,
  ): Promise<StoreSalesmanDashboardResponse> {
    const all = await this.getAllSalesmenDashboard(options);
    const salesman = all.salesmen.find((s) => s.salesmanId === options.salesmanId.trim());
    if (!salesman) {
      throw new NotFoundException(`Salesman '${options.salesmanId}' not found in period`);
    }

    const filtered = all.recentInvoices.filter((row) => this.matchesSalesmanRow(row, salesman));

    return {
      store: all.store,
      period: all.period,
      salesman,
      recentInvoices: filtered.slice(0, options.invoiceLimit),
    };
  }

  private matchesSalesmanRow(row: StoreSalesmenInvoiceRow, salesman: StoreSalesmenSalesmanRow): boolean {
    if (salesman.salesmanId === LEGACY_SALESMAN_ID) {
      return !row.salesmanCode;
    }
    return row.salesmanCode === salesman.salesmanCode
      || (!!salesman.salesmanName && row.salesmanName === salesman.salesmanName);
  }

  private resolveRange(options: StoreSalesmenDashboardOptions) {
    try {
      return resolveDateRange({
        period: options.period,
        ...(options.from !== undefined ? { from: options.from } : {}),
        ...(options.to !== undefined ? { to: options.to } : {}),
        year: options.year,
        month: options.month,
      });
    } catch (err: unknown) {
      throw new BadRequestException(err instanceof Error ? err.message : String(err));
    }
  }

  private async loadInvoices(storeCode: string, range: ReturnType<typeof resolveDateRange>) {
    return (await this.invoiceModel
      .find(buildStoreSalePayloadTimeFilter(storeCode, range))
      .sort({ createdAt: -1 })
      .lean()) as InvoiceLean[];
  }

  private aggregateSalesmen(invoices: InvoiceLean[]): Map<string, SalesmanGroup> {
    const groups = new Map<string, SalesmanGroup>();

    for (const doc of invoices) {
      const payload = (doc.payload ?? {}) as Record<string, unknown>;
      const group = this.resolveSalesmanGroup(payload);
      const existing = groups.get(group.salesmanId) ?? {
        salesmanId: group.salesmanId,
        salesmanCode: group.salesmanCode,
        salesmanName: group.salesmanName,
        invoices: 0,
        itemsSold: 0,
        totalBillAmount: 0,
      };

      existing.invoices += 1;
      existing.itemsSold = roundMoney(existing.itemsSold + sumInvoiceLineQty(payload));
      existing.totalBillAmount = roundMoney(existing.totalBillAmount + parseInvoiceNet(payload));
      groups.set(group.salesmanId, existing);
    }

    return groups;
  }

  private resolveSalesmanGroup(payload: Record<string, unknown>): SalesmanGroup {
    const salesmanId = readString(payload.salesmanId);
    const salesmanCode = readString(payload.salesmanCode) ?? null;
    const salesmanName = readString(payload.salesman) ?? readString(payload.salesmanName) ?? '(No salesman)';

    if (salesmanId) {
      return {
        salesmanId,
        salesmanCode,
        salesmanName,
        invoices: 0,
        itemsSold: 0,
        totalBillAmount: 0,
      };
    }

    if (salesmanCode) {
      return {
        salesmanId: `code:${salesmanCode}`,
        salesmanCode,
        salesmanName,
        invoices: 0,
        itemsSold: 0,
        totalBillAmount: 0,
      };
    }

    if (salesmanName && salesmanName !== '(No salesman)') {
      return {
        salesmanId: `name:${salesmanName}`,
        salesmanCode: null,
        salesmanName,
        invoices: 0,
        itemsSold: 0,
        totalBillAmount: 0,
      };
    }

    return {
      salesmanId: LEGACY_SALESMAN_ID,
      salesmanCode: null,
      salesmanName: 'Legacy bills',
      invoices: 0,
      itemsSold: 0,
      totalBillAmount: 0,
    };
  }

  private buildRecentInvoices(invoices: InvoiceLean[], limit: number): StoreSalesmenInvoiceRow[] {
    return invoices.slice(0, limit).map((doc) => {
      const payload = (doc.payload ?? {}) as Record<string, unknown>;
      const occurred = parseOccurredAt(payload, doc.createdAt);
      return {
        billNo: readString(payload.billNo) ?? doc.invoiceNo,
        occurredAt: occurred?.toISOString() ?? new Date(doc.createdAt ?? Date.now()).toISOString(),
        salesmanCode: readString(payload.salesmanCode) ?? null,
        salesmanName: readString(payload.salesman) ?? readString(payload.salesmanName) ?? null,
        customerName: readString(payload.customerName) ?? null,
        qty: sumInvoiceLineQty(payload),
        payable: parseInvoiceNet(payload),
      };
    });
  }
}
