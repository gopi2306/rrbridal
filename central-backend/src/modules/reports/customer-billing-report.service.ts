import { BadRequestException, Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import type { Model } from 'mongoose';
import { TABULAR_EXPORT_MAX_ROWS } from '../../common/tabular-export';
import { resolveDashboardStore } from '../dashboard/dashboard-store.util';
import {
  buildStoreSalePayloadTimeFilter,
  BUSINESS_TZ_IANA,
  readString,
  resolveBillsListDateRange,
} from '../dashboard/store-sales-payload.util';
import { Store, StoreDocument } from '../stores/schemas/store.schema';
import { StoreInvoice, StoreInvoiceDocument } from '../store-sales/schemas/store-invoice.schema';
import {
  StoreSaleReturn,
  StoreSaleReturnDocument,
} from '../store-sales/schemas/store-sale-return.schema';
import { aggregateCustomerBilling } from './customer-billing-report.util';
import type { CustomerBillingReportResponse } from './customer-billing-report.types';
import { CustomerBillingReportQueryDto } from './dto/customer-billing-report-query.dto';

function escapeRegex(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function exactRegex(value: string): RegExp {
  return new RegExp(`^${escapeRegex(value.trim())}$`, 'i');
}

function phoneRegex(value: string): RegExp {
  const digits = value.replace(/\D/g, '');
  return digits
    ? new RegExp(digits.split('').map(escapeRegex).join('\\D*'))
    : new RegExp(escapeRegex(value.trim()), 'i');
}

@Injectable()
export class CustomerBillingReportService {
  constructor(
    @InjectModel(Store.name) private readonly storeModel: Model<StoreDocument>,
    @InjectModel(StoreInvoice.name) private readonly invoiceModel: Model<StoreInvoiceDocument>,
    @InjectModel(StoreSaleReturn.name) private readonly returnModel: Model<StoreSaleReturnDocument>,
  ) {}

  async buildReport(query: CustomerBillingReportQueryDto): Promise<CustomerBillingReportResponse> {
    const store = await resolveDashboardStore(this.storeModel, query.storeCode);
    const range = this.resolveRange(query);
    const filters: Record<string, unknown>[] = [
      buildStoreSalePayloadTimeFilter(store.code, range),
      {
        $or: [
          { 'payload.status': { $regex: /^posted$/i } },
          { 'payload.status': { $exists: false } },
          { 'payload.status': null },
          { 'payload.status': '' },
        ],
      },
    ];

    const posCounter = query.posCounter?.trim();
    if (posCounter) {
      const exact = exactRegex(posCounter);
      filters.push({ $or: [{ posCounter: exact }, { 'payload.posCounter': exact }] });
    }
    const customerCode = query.customerCode?.trim();
    if (customerCode) filters.push({ 'payload.customerCode': exactRegex(customerCode) });

    const customerPhone = query.customerPhone?.trim();
    if (customerPhone) filters.push({ 'payload.customerPhone': phoneRegex(customerPhone) });

    const customerSearch = query.customerSearch?.trim();
    if (customerSearch) {
      const contains = new RegExp(escapeRegex(customerSearch), 'i');
      const phone = phoneRegex(customerSearch);
      filters.push({
        $or: [
          { 'payload.customerCode': contains },
          { 'payload.customerName': contains },
          { 'payload.customerPhone': phone },
        ],
      });
    }

    const filter = { $and: filters };
    const limit = query.limit ?? TABULAR_EXPORT_MAX_ROWS;
    const total = await this.invoiceModel.countDocuments(filter);
    const invoices = await this.invoiceModel
      .find(filter)
      .sort({ createdAt: -1, invoiceNo: -1 })
      .limit(limit)
      .lean();

    const billNos = new Set<string>();
    for (const invoice of invoices) {
      billNos.add(invoice.invoiceNo);
      const payloadBillNo = readString((invoice.payload ?? {}).billNo);
      if (payloadBillNo) billNos.add(payloadBillNo);
    }

    const returns = billNos.size
      ? await this.returnModel
          .find({
            storeId: store.code,
            'payload.originalBillNo': { $in: [...billNos] },
          })
          .sort({ createdAt: 1 })
          .lean()
      : [];

    const returnsByBill = new Map<string, typeof returns>();
    for (const doc of returns) {
      const originalBillNo = readString((doc.payload ?? {}).originalBillNo);
      if (!originalBillNo) continue;
      const list = returnsByBill.get(originalBillNo) ?? [];
      list.push(doc);
      returnsByBill.set(originalBillNo, list);
    }

    const aggregated = aggregateCustomerBilling(invoices, returnsByBill);
    return {
      period: {
        from: range.fromYmd,
        to: range.toYmd,
        timezone: BUSINESS_TZ_IANA,
        storeCode: store.code,
        storeName: store.name,
        ...(posCounter ? { posCounter } : {}),
      },
      filters: {
        ...(customerSearch ? { customerSearch } : {}),
        ...(customerCode ? { customerCode } : {}),
        ...(customerPhone ? { customerPhone } : {}),
      },
      limit,
      truncated: total > limit,
      total,
      totals: aggregated.totals,
      data: aggregated.data,
    };
  }

  private resolveRange(query: CustomerBillingReportQueryDto) {
    try {
      const range = resolveBillsListDateRange(query.from, query.to);
      if (range.from.getTime() > range.to.getTime()) {
        throw new Error('from must be on or before to');
      }
      return range;
    } catch (err: unknown) {
      throw new BadRequestException(err instanceof Error ? err.message : String(err));
    }
  }
}
