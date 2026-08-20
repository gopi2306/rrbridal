import { BadRequestException, Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import type { Model } from 'mongoose';
import { Types } from 'mongoose';
import { TABULAR_EXPORT_MAX_ROWS } from '../../common/tabular-export';
import { resolveDashboardStore } from '../dashboard/dashboard-store.util';
import {
  buildStoreSalePayloadTimeFilter,
  BUSINESS_TZ_IANA,
  readString,
  resolveBillsListDateRange,
} from '../dashboard/store-sales-payload.util';
import { Product, ProductDocument } from '../products/schemas/product.schema';
import { Store, StoreDocument } from '../stores/schemas/store.schema';
import { StoreInvoice, StoreInvoiceDocument } from '../store-sales/schemas/store-invoice.schema';
import {
  StoreSaleReturn,
  StoreSaleReturnDocument,
} from '../store-sales/schemas/store-sale-return.schema';
import { Supplier, SupplierDocument } from '../suppliers/schemas/supplier.schema';
import { SkuSalesReportQueryDto } from './dto/sku-sales-report-query.dto';
import type { SkuCatalogEntry, SkuSalesPeriod, SkuSalesRow, SkuSalesTotals } from './sku-sales-report.types';
import { collectSkuSales } from './sku-sales-report.util';

function escapeRegex(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function exactRegex(value: string): RegExp {
  return new RegExp(`^${escapeRegex(value.trim())}$`, 'i');
}

function supplierIdOf(value: unknown): string | null {
  if (!value) return null;
  if (typeof value === 'string') return value.trim() || null;
  if (typeof value === 'object' && value && '_id' in value) {
    const id = (value as { _id?: unknown })._id;
    return id ? String(id) : null;
  }
  return String(value);
}

function supplierNameOf(value: unknown): string | undefined {
  if (!value || typeof value !== 'object') return undefined;
  const name = (value as { name?: unknown }).name;
  return typeof name === 'string' && name.trim() ? name.trim() : undefined;
}

export type LoadedSkuSales = {
  period: SkuSalesPeriod;
  invoiceLimit: number;
  invoiceTotal: number;
  invoiceTruncated: boolean;
  search?: string;
  rows: SkuSalesRow[];
  totals: SkuSalesTotals;
};

@Injectable()
export class SkuSalesReportLoader {
  constructor(
    @InjectModel(Store.name) private readonly storeModel: Model<StoreDocument>,
    @InjectModel(StoreInvoice.name) private readonly invoiceModel: Model<StoreInvoiceDocument>,
    @InjectModel(StoreSaleReturn.name) private readonly returnModel: Model<StoreSaleReturnDocument>,
    @InjectModel(Product.name) private readonly productModel: Model<ProductDocument>,
    @InjectModel(Supplier.name) private readonly supplierModel: Model<SupplierDocument>,
  ) {}

  async loadSkuSales(query: SkuSalesReportQueryDto): Promise<LoadedSkuSales> {
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

    const filter = { $and: filters };
    const invoiceLimit = query.limit && query.limit > 0
      ? Math.min(query.limit, TABULAR_EXPORT_MAX_ROWS)
      : TABULAR_EXPORT_MAX_ROWS;
    const invoiceTotal = await this.invoiceModel.countDocuments(filter);
    const invoices = await this.invoiceModel
      .find(filter)
      .sort({ createdAt: -1, invoiceNo: -1 })
      .limit(invoiceLimit)
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

    const skus = new Set<string>();
    for (const invoice of invoices) {
      const lines = (invoice.payload ?? {}).lines;
      if (!Array.isArray(lines)) continue;
      for (const line of lines) {
        if (!line || typeof line !== 'object') continue;
        const sku =
          readString((line as Record<string, unknown>).sku) ??
          readString((line as Record<string, unknown>).productCode);
        if (sku) skus.add(sku);
      }
    }
    for (const doc of returns) {
      const payload = (doc.payload ?? {}) as Record<string, unknown>;
      const lines = payload.returnLines ?? payload.lines;
      if (!Array.isArray(lines)) continue;
      for (const line of lines) {
        if (!line || typeof line !== 'object') continue;
        const sku =
          readString((line as Record<string, unknown>).sku) ??
          readString((line as Record<string, unknown>).productCode);
        if (sku) skus.add(sku);
      }
    }

    const products = skus.size
      ? await this.productModel
          .find({ sku: { $in: [...skus] } })
          .select('sku itemName supplierNameId')
          .populate('supplierNameId', 'name')
          .lean()
      : [];

    const catalog = new Map<string, SkuCatalogEntry>();
    const supplierIds = new Set<string>();
    for (const product of products) {
      const supplierNameId = supplierIdOf(product.supplierNameId);
      const populatedName = supplierNameOf(product.supplierNameId);
      catalog.set(product.sku, {
        sku: product.sku,
        itemName: product.itemName,
        supplierNameId,
        ...(populatedName ? { supplierName: populatedName } : {}),
      });
      if (supplierNameId) supplierIds.add(supplierNameId);
    }

    const supplierNames = new Map<string, string>();
    const missingIds = [...supplierIds].filter((id) => Types.ObjectId.isValid(id));
    if (missingIds.length) {
      const suppliers = await this.supplierModel
        .find({ _id: { $in: missingIds.map((id) => new Types.ObjectId(id)) } })
        .select('name')
        .lean();
      for (const supplier of suppliers) {
        supplierNames.set(String(supplier._id), supplier.name);
      }
    }

    const aggregated = collectSkuSales(invoices, returns, catalog, supplierNames);
    const search = query.search?.trim();
    return {
      period: {
        from: range.fromYmd,
        to: range.toYmd,
        timezone: BUSINESS_TZ_IANA,
        storeCode: store.code,
        storeName: store.name,
        ...(posCounter ? { posCounter } : {}),
      },
      invoiceLimit,
      invoiceTotal,
      invoiceTruncated: invoiceTotal > invoiceLimit,
      ...(search ? { search } : {}),
      rows: aggregated.rows,
      totals: aggregated.totals,
    };
  }

  private resolveRange(query: SkuSalesReportQueryDto) {
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
