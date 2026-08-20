import { Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import type { Model } from 'mongoose';
import { TABULAR_EXPORT_MAX_ROWS } from '../../common/tabular-export';
import { resolveDashboardStore } from '../dashboard/dashboard-store.util';
import {
  BUSINESS_TZ_IANA,
  formatYmdFromParts,
  toBusinessCalendarParts,
} from '../dashboard/store-sales-payload.util';
import { InventoryService } from '../inventory/inventory.service';
import { Product, ProductDocument } from '../products/schemas/product.schema';
import { Store, StoreDocument } from '../stores/schemas/store.schema';
import { GoingOutOfStockReportQueryDto } from './dto/going-out-of-stock-report-query.dto';
import type {
  GoingOutOfStockProductInput,
  GoingOutOfStockReportResponse,
} from './going-out-of-stock-report.util';
import {
  collectGoingOutOfStock,
  filterGoingOutOfStockRows,
  totalsGoingOutOfStock,
} from './going-out-of-stock-report.util';

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

@Injectable()
export class GoingOutOfStockReportService {
  constructor(
    private readonly inventoryService: InventoryService,
    @InjectModel(Store.name) private readonly storeModel: Model<StoreDocument>,
    @InjectModel(Product.name) private readonly productModel: Model<ProductDocument>,
  ) {}

  async buildReport(query: GoingOutOfStockReportQueryDto): Promise<GoingOutOfStockReportResponse> {
    const store = await resolveDashboardStore(this.storeModel, query.storeCode);
    const [storeQtyMap, products] = await Promise.all([
      this.inventoryService.getStoreSkuQtyMap(store.code),
      this.loadProducts(),
    ]);

    const allRows = collectGoingOutOfStock(products, storeQtyMap);
    const search = query.search?.trim();
    const status = query.status;
    const filtered = filterGoingOutOfStockRows(allRows, {
      ...(search ? { search } : {}),
      ...(status ? { status } : {}),
    });
    const limit = query.limit ?? TABULAR_EXPORT_MAX_ROWS;
    const data = filtered.slice(0, limit);
    const asOfParts = toBusinessCalendarParts(new Date());
    const asOf = formatYmdFromParts(asOfParts.y, asOfParts.m, asOfParts.day);

    return {
      period: {
        asOf,
        timezone: BUSINESS_TZ_IANA,
        storeCode: store.code,
        storeName: store.name,
      },
      filters: {
        ...(search ? { search } : {}),
        ...(status ? { status } : {}),
      },
      limit,
      truncated: filtered.length > limit,
      total: filtered.length,
      totals: totalsGoingOutOfStock(data),
      data,
    };
  }

  private async loadProducts(): Promise<GoingOutOfStockProductInput[]> {
    const products = await this.productModel
      .find({
        isActive: true,
        $or: [
          { minimumShelfFit: { $exists: true, $ne: null } },
          { minStock: { $exists: true, $ne: null } },
          { reorderLevel: { $exists: true, $ne: null } },
        ],
      })
      .select('sku itemName minimumShelfFit minStock reorderLevel supplierNameId')
      .populate('supplierNameId', 'name')
      .lean();

    return products.map((product) => ({
      sku: product.sku,
      itemName: product.itemName,
      minimumShelfFit: product.minimumShelfFit,
      minStock: product.minStock,
      reorderLevel: product.reorderLevel,
      supplierId: supplierIdOf(product.supplierNameId),
      supplierName: supplierNameOf(product.supplierNameId),
    }));
  }
}
