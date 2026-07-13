import {
  BadRequestException,
  Injectable,
  NotFoundException,
  PayloadTooLargeException,
} from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model, Types } from 'mongoose';
import { applyObjectIdRefFilter } from '../../common/object-id.util';
import { roundMoney } from '../../common/money.util';
import { TABULAR_EXPORT_MAX_ROWS } from '../../common/tabular-export';
import { computeMarginPercent } from '../dashboard/store-sales-attribution.util';
import {
  UNMAPPED_VENDOR_ID,
  UNMAPPED_VENDOR_NAME,
} from '../dashboard/store-vendors-sales-dashboard.types';
import { Product, ProductDocument } from '../products/schemas/product.schema';
import { Supplier, SupplierDocument } from '../suppliers/schemas/supplier.schema';
import { Store, StoreDocument } from '../stores/schemas/store.schema';
import type { SupplierWiseReportQueryDto } from './dto/supplier-wise-report-query.dto';
import { InventoryService } from './inventory.service';
import type {
  SupplierWiseProductReportResponse,
  SupplierWiseProductReportRow,
  SupplierWiseReportFilters,
  SupplierWiseReportOptions,
  SupplierWiseReportResponse,
  SupplierWiseReportRow,
  SupplierWiseReportStoreContext,
  SupplierWiseReportSummary,
} from './supplier-wise-report.types';

type ProductIndexRow = {
  sku: string;
  itemName: string;
  supplierId: string;
  costPrice: number;
  sellingPrice: number;
};

function escapeRegex(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function stockValueMetrics(stockQty: number, costPrice: number, sellingPrice: number) {
  const costValue = roundMoney(stockQty * costPrice);
  const sellingValue = roundMoney(stockQty * sellingPrice);
  const margin = roundMoney(sellingValue - costValue);
  return {
    stockQty: roundMoney(stockQty),
    costValue,
    sellingValue,
    margin,
    marginPercent: computeMarginPercent(margin, costValue),
  };
}

@Injectable()
export class SupplierWiseReportService {
  constructor(
    @InjectModel(Product.name) private readonly productModel: Model<ProductDocument>,
    @InjectModel(Supplier.name) private readonly supplierModel: Model<SupplierDocument>,
    @InjectModel(Store.name) private readonly storeModel: Model<StoreDocument>,
    private readonly inventoryService: InventoryService,
  ) {}

  async buildSupplierReport(query: SupplierWiseReportQueryDto): Promise<SupplierWiseReportResponse> {
    const options = this.toOptions(query);
    const filters = await this.buildReportFilters(options);
    const products = await this.loadProductIndex(options);
    const stockBySku = await this.loadStockBySku(options.scope, options.storeId);
    const supplierNames = await this.loadSupplierNames(products, options.search);

    const supplierAgg = new Map<
      string,
      {
        stockQty: number;
        costValue: number;
        sellingValue: number;
        productSkus: Set<string>;
      }
    >();

    for (const product of products) {
      const stockQty = stockBySku.get(product.sku) ?? 0;
      if (stockQty <= 0) continue;

      const values = stockValueMetrics(stockQty, product.costPrice, product.sellingPrice);
      const bucket = supplierAgg.get(product.supplierId) ?? {
        stockQty: 0,
        costValue: 0,
        sellingValue: 0,
        productSkus: new Set<string>(),
      };
      bucket.stockQty += values.stockQty;
      bucket.costValue += values.costValue;
      bucket.sellingValue += values.sellingValue;
      bucket.productSkus.add(product.sku);
      supplierAgg.set(product.supplierId, bucket);
    }

    const rows: SupplierWiseReportRow[] = [...supplierAgg.entries()]
      .map(([supplierId, bucket]) => {
        const margin = roundMoney(bucket.sellingValue - bucket.costValue);
        return {
          supplierId,
          supplierName: supplierNames.get(supplierId) ?? UNMAPPED_VENDOR_NAME,
          productCount: bucket.productSkus.size,
          stockQty: roundMoney(bucket.stockQty),
          costValue: roundMoney(bucket.costValue),
          sellingValue: roundMoney(bucket.sellingValue),
          margin,
          marginPercent: computeMarginPercent(margin, bucket.costValue),
        };
      })
      .sort((a, b) => {
        if (b.stockQty !== a.stockQty) return b.stockQty - a.stockQty;
        return a.supplierName.localeCompare(b.supplierName);
      });

    if (rows.length > TABULAR_EXPORT_MAX_ROWS) {
      throw new PayloadTooLargeException(
        `Report exceeds maximum of ${TABULAR_EXPORT_MAX_ROWS} suppliers. Narrow filters and try again.`,
      );
    }

    return {
      filters,
      summary: this.buildSupplierSummary(rows),
      rows,
    };
  }

  async buildProductReport(
    supplierId: string,
    query: SupplierWiseReportQueryDto,
  ): Promise<SupplierWiseProductReportResponse> {
    const options = this.toOptions({ ...query, supplierId });
    const filters = await this.buildReportFilters(options);
    const supplier = await this.resolveSupplier(supplierId);
    const products = await this.loadProductIndex(options);
    const stockBySku = await this.loadStockBySku(options.scope, options.storeId);

    const rows: SupplierWiseProductReportRow[] = products
      .map((product) => {
        const stockQty = stockBySku.get(product.sku) ?? 0;
        if (stockQty <= 0) return null;
        const values = stockValueMetrics(stockQty, product.costPrice, product.sellingPrice);
        return {
          sku: product.sku,
          productName: product.itemName,
          ...values,
        };
      })
      .filter((row): row is SupplierWiseProductReportRow => row !== null)
      .sort((a, b) => {
        if (b.stockQty !== a.stockQty) return b.stockQty - a.stockQty;
        return a.productName.localeCompare(b.productName);
      });

    if (rows.length > TABULAR_EXPORT_MAX_ROWS) {
      throw new PayloadTooLargeException(
        `Report exceeds maximum of ${TABULAR_EXPORT_MAX_ROWS} products. Narrow filters and try again.`,
      );
    }

    return {
      supplier: { id: supplier.id, name: supplier.name },
      filters,
      summary: this.buildProductSummary(rows),
      rows,
    };
  }

  toOptions(query: SupplierWiseReportQueryDto): SupplierWiseReportOptions {
    return {
      scope: query.scope,
      ...(query.storeId !== undefined && query.storeId !== '' ? { storeId: query.storeId } : {}),
      ...(query.search !== undefined && query.search !== '' ? { search: query.search } : {}),
      ...(query.brandId !== undefined && query.brandId !== '' ? { brandId: query.brandId } : {}),
      ...(query.categoryId !== undefined && query.categoryId !== ''
        ? { categoryId: query.categoryId }
        : {}),
      ...(query.supplierId !== undefined && query.supplierId !== ''
        ? { supplierId: query.supplierId }
        : {}),
    };
  }

  private async buildReportFilters(options: SupplierWiseReportOptions): Promise<SupplierWiseReportFilters> {
    const store = await this.resolveStoreContext(options.storeId);
    return {
      scope: options.scope,
      store,
      ...(options.search ? { search: options.search } : {}),
      ...(options.brandId ? { brandId: options.brandId } : {}),
      ...(options.categoryId ? { categoryId: options.categoryId } : {}),
      ...(options.supplierId ? { supplierId: options.supplierId } : {}),
    };
  }

  private async resolveStoreContext(storeId?: string): Promise<SupplierWiseReportStoreContext> {
    if (!storeId?.trim()) {
      return { code: 'all-stores', name: 'All stores', label: 'All stores' };
    }
    const code = storeId.trim().toLowerCase();
    const store = await this.storeModel.findOne({ code }).lean();
    if (!store) {
      return { code, name: code, label: code };
    }
    return {
      code: store.code,
      name: store.name,
      label: `${store.name} (${store.code})`,
    };
  }

  private async resolveSupplier(supplierId: string): Promise<{ id: string; name: string }> {
    if (supplierId === UNMAPPED_VENDOR_ID) {
      return { id: UNMAPPED_VENDOR_ID, name: UNMAPPED_VENDOR_NAME };
    }
    if (!Types.ObjectId.isValid(supplierId)) {
      throw new BadRequestException('Invalid supplier id');
    }
    const supplier = await this.supplierModel.findById(supplierId).select('name').lean();
    if (!supplier) throw new NotFoundException('Supplier not found');
    return { id: String(supplier._id), name: supplier.name };
  }

  private async loadProductIndex(options: SupplierWiseReportOptions): Promise<ProductIndexRow[]> {
    const filter: FilterQuery<ProductDocument> = { isActive: { $ne: false } };

    applyObjectIdRefFilter(filter, 'categoryId', options.categoryId);
    applyObjectIdRefFilter(filter, 'brandId', options.brandId);

    if (options.supplierId) {
      if (options.supplierId === UNMAPPED_VENDOR_ID) {
        filter.$or = [{ supplierNameId: { $exists: false } }, { supplierNameId: null }];
      } else {
        applyObjectIdRefFilter(filter, 'supplierNameId', options.supplierId);
      }
    }

    const search = options.search?.trim();
    if (search) {
      const rx = { $regex: escapeRegex(search), $options: 'i' };
      const matchingSuppliers = await this.supplierModel
        .find({ name: rx, isActive: { $ne: false } })
        .select('_id')
        .lean();
      const supplierIds = matchingSuppliers.map((s) => s._id);

      const searchOr: FilterQuery<ProductDocument>[] = [
        { itemName: rx },
        { shortName: rx },
        { alias: rx },
        { sku: rx },
        { upcEanCode: rx },
      ];
      if (supplierIds.length > 0) {
        searchOr.push({ supplierNameId: { $in: supplierIds } });
      }

      if (filter.$or) {
        filter.$and = [{ $or: filter.$or }, { $or: searchOr }];
        delete filter.$or;
      } else {
        filter.$or = searchOr;
      }
    }

    const docs = await this.productModel
      .find(filter)
      .select('sku itemName supplierNameId costPrice sellingPrice')
      .lean();

    return docs.map((doc) => ({
      sku: doc.sku,
      itemName: doc.itemName,
      supplierId: doc.supplierNameId ? String(doc.supplierNameId) : UNMAPPED_VENDOR_ID,
      costPrice: doc.costPrice ?? 0,
      sellingPrice: doc.sellingPrice ?? 0,
    }));
  }

  private async loadSupplierNames(
    products: ProductIndexRow[],
    search?: string,
  ): Promise<Map<string, string>> {
    const ids = [...new Set(products.map((p) => p.supplierId).filter((id) => id !== UNMAPPED_VENDOR_ID))];
    const map = new Map<string, string>();
    map.set(UNMAPPED_VENDOR_ID, UNMAPPED_VENDOR_NAME);

    if (ids.length === 0) return map;

    const objectIds = ids.filter((id) => Types.ObjectId.isValid(id)).map((id) => new Types.ObjectId(id));
    const suppliers = await this.supplierModel.find({ _id: { $in: objectIds } }).select('name').lean();
    for (const supplier of suppliers) {
      map.set(String(supplier._id), supplier.name);
    }

    if (search?.trim()) {
      const rx = { $regex: escapeRegex(search.trim()), $options: 'i' };
      const matched = await this.supplierModel.find({ name: rx }).select('name').lean();
      for (const supplier of matched) {
        map.set(String(supplier._id), supplier.name);
      }
    }

    return map;
  }

  private async loadStockBySku(
    scope: SupplierWiseReportOptions['scope'],
    storeId?: string,
  ): Promise<Map<string, number>> {
    if (scope === 'warehouse') {
      const maps = await this.inventoryService.getWarehouseSkuQtyMaps();
      return maps.warehouseBySku;
    }

    const sid = storeId?.trim().toLowerCase();
    if (sid) {
      return await this.inventoryService.getStoreSkuQtyMap(sid);
    }

    const allStores = await this.inventoryService.getAllStoreSkuQtyMaps();
    const totals = new Map<string, number>();
    for (const storeMap of allStores.values()) {
      for (const [sku, qty] of storeMap) {
        totals.set(sku, (totals.get(sku) ?? 0) + qty);
      }
    }
    return totals;
  }

  private buildSupplierSummary(rows: SupplierWiseReportRow[]): SupplierWiseReportSummary {
    const totalCostValue = roundMoney(rows.reduce((s, r) => s + r.costValue, 0));
    const totalSellingValue = roundMoney(rows.reduce((s, r) => s + r.sellingValue, 0));
    const totalMargin = roundMoney(totalSellingValue - totalCostValue);
    return {
      supplierCount: rows.length,
      productCount: rows.reduce((s, r) => s + r.productCount, 0),
      stockQty: roundMoney(rows.reduce((s, r) => s + r.stockQty, 0)),
      totalCostValue,
      totalSellingValue,
      totalMargin,
      marginPercent: computeMarginPercent(totalMargin, totalCostValue),
    };
  }

  private buildProductSummary(rows: SupplierWiseProductReportRow[]) {
    const costValue = roundMoney(rows.reduce((s, r) => s + r.costValue, 0));
    const sellingValue = roundMoney(rows.reduce((s, r) => s + r.sellingValue, 0));
    const margin = roundMoney(sellingValue - costValue);
    return {
      productCount: rows.length,
      stockQty: roundMoney(rows.reduce((s, r) => s + r.stockQty, 0)),
      costValue,
      sellingValue,
      margin,
      marginPercent: computeMarginPercent(margin, costValue),
    };
  }
}
