import { BadRequestException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model, Types } from 'mongoose';
import { roundMoney } from '../../common/money.util';
import { Category, CategoryDocument } from '../categories/schemas/category.schema';
import { Product, ProductDocument } from '../products/schemas/product.schema';
import { StoreInvoice, StoreInvoiceDocument } from '../store-sales/schemas/store-invoice.schema';
import { StoreSaleReturn, StoreSaleReturnDocument } from '../store-sales/schemas/store-sale-return.schema';
import { Supplier, SupplierDocument } from '../suppliers/schemas/supplier.schema';
import { resolveDashboardStore } from './dashboard-store.util';
import { Store, StoreDocument } from '../stores/schemas/store.schema';
import {
  aggregateMarginLines,
  bucketKeyForDate,
  buildDashboardPeriod,
  buildStoreSalePayloadTimeFilter,
  computeMarginPercentage,
  countVendorLinesInPayload,
  filterMarginLinesBySkuSet,
  isInRange,
  labelForBucketKey,
  parseInvoiceLines,
  parseInvoiceMarginLines,
  parseOccurredAt,
  parseReturnLines,
  parseReturnMarginLines,
  readString,
  resolveDateRange,
  sumVendorGrossFromLines,
  type LineAgg,
  type LineMarginRow,
} from './store-sales-payload.util';
import type {
  StoreVendorsSalesDashboardOptions,
  StoreVendorsSalesDashboardResponse,
  StoreVendorsSalesInvoiceRow,
  StoreVendorsSalesReturnRow,
  StoreVendorsSalesVendorRow,
} from './store-vendors-sales-dashboard.types';
import {
  UNMAPPED_VENDOR_ID,
  UNMAPPED_VENDOR_NAME,
} from './store-vendors-sales-dashboard.types';
import type {
  StoreVendorSalesCategoryMixRow,
  StoreVendorSalesDashboardOptions,
  StoreVendorSalesDashboardResponse,
  StoreVendorSalesDetailRow,
  StoreVendorSalesInvoiceRow,
  StoreVendorSalesReturnBreakdownRow,
  StoreVendorSalesReturnRow,
  StoreVendorSalesTopProductRow,
} from './store-vendor-sales-dashboard.types';

type SupplierSkuMeta = {
  costPrice: number;
  categoryId: string;
  itemName: string;
};

type BucketAgg = {
  invoices: number;
  items: number;
  gross: number;
  net: number;
  returnsCount: number;
  returnValue: number;
};

type SignedMarginLine = LineMarginRow & { sign: 1 | -1 };

type ProductAgg = {
  description: string;
  units: number;
  salesAmount: number;
  costValue: number;
};

type VendorBucket = {
  supplierId: string;
  salesQty: number;
  costValue: number;
  salesAmount: number;
};

@Injectable()
export class StoreVendorSalesDashboardService {
  constructor(
    @InjectModel(StoreInvoice.name) private readonly invoiceModel: Model<StoreInvoiceDocument>,
    @InjectModel(StoreSaleReturn.name) private readonly returnModel: Model<StoreSaleReturnDocument>,
    @InjectModel(Store.name) private readonly storeModel: Model<StoreDocument>,
    @InjectModel(Product.name) private readonly productModel: Model<ProductDocument>,
    @InjectModel(Supplier.name) private readonly supplierModel: Model<SupplierDocument>,
    @InjectModel(Category.name) private readonly categoryModel: Model<CategoryDocument>,
  ) {}

  async getStoreVendorSalesDashboard(
    options: StoreVendorSalesDashboardOptions,
  ): Promise<StoreVendorSalesDashboardResponse> {
    const store = await this.resolveStore(options.storeId);
    const supplier = await this.resolveSupplier(options.supplierId);
    const { skuSet, skuMeta } = await this.buildSupplierSkuIndex(supplier.id);

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

    const [invoices, returns] = await Promise.all([
      this.invoiceModel.find(buildStoreSalePayloadTimeFilter(store.code, range)).lean(),
      this.returnModel.find(buildStoreSalePayloadTimeFilter(store.code, range)).lean(),
    ]);

    const buckets = new Map<string, BucketAgg>();
    const productTotals = new Map<string, ProductAgg>();
    const categoryPieces = new Map<string, number>();
    const marginLines: SignedMarginLine[] = [];
    const recentInvoices: StoreVendorSalesInvoiceRow[] = [];
    const returnRows: StoreVendorSalesReturnRow[] = [];

    let grossSales = 0;
    let netSales = 0;
    let invoiceCount = 0;
    let itemsSold = 0;
    let returnValue = 0;
    let returnsCount = 0;
    let returnQtyTotal = 0;

    const costBySku = new Map<string, number>(
      [...skuMeta.entries()].map(([sku, meta]) => [sku, meta.costPrice]),
    );

    for (const inv of invoices) {
      const payload = (inv.payload ?? {}) as Record<string, unknown>;
      const occurred = parseOccurredAt(payload, this.docTimestamp(inv));
      if (!occurred || !isInRange(occurred, range)) continue;

      const vendorLines = filterMarginLinesBySkuSet(parseInvoiceMarginLines(payload), skuSet);
      const vendorQty = parseInvoiceLines(payload)
        .filter((l) => skuSet.has(l.sku))
        .reduce((s, l) => s + l.qty, 0);
      if (vendorQty === 0) continue;

      const vendorGross = sumVendorGrossFromLines(payload, skuSet);
      const billMargin = aggregateMarginLines(vendorLines, costBySku, 1);

      grossSales += vendorGross;
      netSales += billMargin.totalSellingValue;
      invoiceCount += 1;
      itemsSold += vendorQty;

      const key = bucketKeyForDate(occurred, range.bucketByMonth);
      this.ensureBucket(buckets, key);
      const b = buckets.get(key)!;
      b.invoices += 1;
      b.items += vendorQty;
      b.gross += vendorGross;
      b.net += billMargin.totalSellingValue;

      for (const line of vendorLines) {
        marginLines.push({ ...line, sign: 1 });
        this.accumulateProduct(productTotals, line, skuMeta, costBySku, 1);
        this.accumulateCategory(categoryPieces, line.sku, skuMeta, line.qty);
      }

      recentInvoices.push({
        billNo: inv.invoiceNo,
        occurredAt: occurred.toISOString(),
        qty: vendorQty,
        costValue: billMargin.totalCostValue,
        salesAmount: billMargin.totalSellingValue,
        margin: billMargin.salesMargin,
      });
    }

    for (const ret of returns) {
      const payload = (ret.payload ?? {}) as Record<string, unknown>;
      const occurred = parseOccurredAt(payload, this.docTimestamp(ret));
      if (!occurred || !isInRange(occurred, range)) continue;

      const vendorReturnQtyLines = parseReturnLines(payload).filter((l) => skuSet.has(l.sku));
      if (vendorReturnQtyLines.length === 0) continue;

      const vendorLines = filterMarginLinesBySkuSet(parseReturnMarginLines(payload), skuSet);
      const returnMargin = aggregateMarginLines(vendorLines, costBySku, 1);
      const vendorQty = vendorReturnQtyLines.reduce((s, l) => s + l.qty, 0);

      returnValue += returnMargin.totalSellingValue;
      returnsCount += 1;
      returnQtyTotal += vendorQty;

      const key = bucketKeyForDate(occurred, range.bucketByMonth);
      this.ensureBucket(buckets, key);
      const b = buckets.get(key)!;
      b.returnsCount += 1;
      b.returnValue += returnMargin.totalSellingValue;

      for (const line of vendorLines) {
        marginLines.push({ ...line, sign: -1 });
        this.accumulateProduct(productTotals, line, skuMeta, costBySku, -1);
      }

      returnRows.push({
        returnNo: ret.returnNo,
        originalBillNo: readString(payload.originalBillNo) ?? null,
        occurredAt: occurred.toISOString(),
        qty: vendorQty,
        returnValue: returnMargin.totalSellingValue,
        lineCount: countVendorLinesInPayload(payload, skuSet, { returnLine: true }),
      });
    }

    const marginSummary = this.computeMarginSummary(marginLines, costBySku);
    const salesDetails = this.buildSalesDetails(buckets, range.bucketByMonth);
    const returnBreakdown = this.buildReturnBreakdown(buckets, range.bucketByMonth);
    const topProducts = this.buildTopProducts(productTotals, options.topProductLimit);
    const categoryMix = await this.buildCategoryMix(categoryPieces);

    recentInvoices.sort(
      (a, b) => new Date(b.occurredAt).getTime() - new Date(a.occurredAt).getTime(),
    );
    returnRows.sort(
      (a, b) => new Date(b.occurredAt).getTime() - new Date(a.occurredAt).getTime(),
    );

    const itemsSoldClamped = Math.max(0, itemsSold);

    return {
      store,
      supplier,
      period: buildDashboardPeriod(options.period, range),
      summary: {
        grossSales: roundMoney(grossSales),
        netSales: roundMoney(netSales),
        returnValue: roundMoney(returnValue),
        returnsCount,
        invoices: invoiceCount,
        itemsSold: itemsSoldClamped,
        totalCostValue: marginSummary.totalCostValue,
        totalSellingValue: marginSummary.totalSellingValue,
        salesMargin: marginSummary.salesMargin,
        marginPercentage: marginSummary.marginPercentage,
      },
      marginInsights: {
        marginPercentage: marginSummary.marginPercentage,
        avgSalePerUnit:
          itemsSoldClamped > 0
            ? roundMoney(marginSummary.totalSellingValue / itemsSoldClamped)
            : 0,
        avgCostPerUnit:
          itemsSoldClamped > 0 ? roundMoney(marginSummary.totalCostValue / itemsSoldClamped) : 0,
        avgInvoiceValue: invoiceCount > 0 ? roundMoney(netSales / invoiceCount) : 0,
        returnsQty: returnQtyTotal,
        returnsValue: roundMoney(returnValue),
      },
      salesDetails,
      returnBreakdown,
      topProducts,
      categoryMix,
      recentInvoices: recentInvoices.slice(0, options.invoiceLimit),
      returns: returnRows.slice(0, options.returnDetailLimit),
    };
  }

  async getAllVendorsSalesDashboard(
    options: StoreVendorsSalesDashboardOptions,
  ): Promise<StoreVendorsSalesDashboardResponse> {
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

    const [invoices, returns, products, suppliers] = await Promise.all([
      this.invoiceModel.find(buildStoreSalePayloadTimeFilter(store.code, range)).lean(),
      this.returnModel.find(buildStoreSalePayloadTimeFilter(store.code, range)).lean(),
      this.productModel.find().select('sku costPrice supplierNameId').lean(),
      this.supplierModel.find().select('name').lean(),
    ]);

    const { skuVendorMap, costBySku } = this.buildGlobalSkuVendorIndex(products);
    const vendorNameById = new Map(suppliers.map((s) => [String(s._id), s.name]));
    vendorNameById.set(UNMAPPED_VENDOR_ID, UNMAPPED_VENDOR_NAME);

    const vendorBuckets = new Map<string, VendorBucket>();
    const recentInvoices: StoreVendorsSalesInvoiceRow[] = [];
    const returnRows: StoreVendorsSalesReturnRow[] = [];

    let invoiceCount = 0;
    let returnsCount = 0;
    let returnValue = 0;

    for (const inv of invoices) {
      const payload = (inv.payload ?? {}) as Record<string, unknown>;
      const occurred = parseOccurredAt(payload, this.docTimestamp(inv));
      if (!occurred || !isInRange(occurred, range)) continue;

      const attributed = this.attributeSaleLines(
        parseInvoiceLines(payload),
        parseInvoiceMarginLines(payload),
        skuVendorMap,
        costBySku,
        1,
      );
      if (attributed.totalQty === 0) continue;

      invoiceCount += 1;
      this.mergeVendorBuckets(vendorBuckets, attributed.byVendor);

      recentInvoices.push({
        billNo: inv.invoiceNo,
        occurredAt: occurred.toISOString(),
        qty: attributed.totalQty,
        mappedQty: attributed.mappedQty,
        unmappedQty: attributed.unmappedQty,
        hasUnmapped: attributed.unmappedQty > 0,
        costValue: roundMoney(attributed.costValue),
        salesAmount: roundMoney(attributed.salesAmount),
        margin: roundMoney(attributed.salesAmount - attributed.costValue),
      });
    }

    for (const ret of returns) {
      const payload = (ret.payload ?? {}) as Record<string, unknown>;
      const occurred = parseOccurredAt(payload, this.docTimestamp(ret));
      if (!occurred || !isInRange(occurred, range)) continue;

      const returnLineAggs = parseReturnLines(payload);
      if (returnLineAggs.length === 0) continue;

      const attributed = this.attributeSaleLines(
        returnLineAggs,
        parseReturnMarginLines(payload),
        skuVendorMap,
        costBySku,
        -1,
      );
      if (attributed.totalQty === 0) continue;

      returnsCount += 1;
      returnValue += Math.abs(attributed.salesAmount);
      this.mergeVendorMarginBuckets(vendorBuckets, attributed.byVendor);

      returnRows.push({
        returnNo: ret.returnNo,
        originalBillNo: readString(payload.originalBillNo) ?? null,
        occurredAt: occurred.toISOString(),
        qty: attributed.totalQty,
        mappedQty: attributed.mappedQty,
        unmappedQty: attributed.unmappedQty,
        hasUnmapped: attributed.unmappedQty > 0,
        returnValue: roundMoney(attributed.salesAmount),
        lineCount: returnLineAggs.length,
      });
    }

    const vendors = this.buildVendorRows(vendorBuckets, vendorNameById);
    const summary = this.buildAllVendorsSummary(vendorBuckets, vendors, {
      invoiceCount,
      returnsCount,
      returnValue,
    });

    recentInvoices.sort(
      (a, b) => new Date(b.occurredAt).getTime() - new Date(a.occurredAt).getTime(),
    );
    returnRows.sort(
      (a, b) => new Date(b.occurredAt).getTime() - new Date(a.occurredAt).getTime(),
    );

    return {
      store,
      period: buildDashboardPeriod(options.period, range),
      summary,
      vendors,
      recentInvoices: recentInvoices.slice(0, options.invoiceLimit),
      returns: returnRows.slice(0, options.returnDetailLimit),
    };
  }

  private buildGlobalSkuVendorIndex(
    products: Array<{ sku: string; costPrice?: number; supplierNameId?: Types.ObjectId | null }>,
  ) {
    const skuVendorMap = new Map<string, string>();
    const costBySku = new Map<string, number>();

    for (const p of products) {
      const supplierId = p.supplierNameId ? String(p.supplierNameId) : UNMAPPED_VENDOR_ID;
      skuVendorMap.set(p.sku, supplierId);
      costBySku.set(p.sku, p.costPrice ?? 0);
    }

    return { skuVendorMap, costBySku };
  }

  private attributeSaleLines(
    qtyLines: readonly LineAgg[],
    marginLines: readonly LineMarginRow[],
    skuVendorMap: ReadonlyMap<string, string>,
    costBySku: ReadonlyMap<string, number>,
    sign: 1 | -1,
  ) {
    const marginBySku = new Map(marginLines.map((l) => [l.sku, l]));
    const byVendor = new Map<string, VendorBucket>();
    let totalQty = 0;
    let mappedQty = 0;
    let unmappedQty = 0;
    let costValue = 0;
    let salesAmount = 0;

    for (const line of qtyLines) {
      if (line.qty <= 0) continue;

      const supplierId = skuVendorMap.get(line.sku) ?? UNMAPPED_VENDOR_ID;
      const margin = marginBySku.get(line.sku);
      const qty = sign * line.qty;
      const costUnit = margin?.costPerUnit
        ? margin.costPerUnit
        : (costBySku.get(line.sku) ?? 0);
      const lineCost = sign * costUnit * line.qty;
      const lineSell = margin ? sign * margin.sellingValue : 0;

      totalQty += line.qty;
      if (supplierId === UNMAPPED_VENDOR_ID) {
        unmappedQty += line.qty;
      } else {
        mappedQty += line.qty;
      }
      costValue += lineCost;
      salesAmount += lineSell;

      const bucket = byVendor.get(supplierId) ?? {
        supplierId,
        salesQty: 0,
        costValue: 0,
        salesAmount: 0,
      };
      bucket.salesQty += qty;
      bucket.costValue += lineCost;
      bucket.salesAmount += lineSell;
      byVendor.set(supplierId, bucket);
    }

    return {
      byVendor,
      totalQty,
      mappedQty,
      unmappedQty,
      costValue,
      salesAmount,
    };
  }

  private mergeVendorBuckets(target: Map<string, VendorBucket>, source: Map<string, VendorBucket>) {
    for (const [id, bucket] of source) {
      const cur = target.get(id) ?? {
        supplierId: id,
        salesQty: 0,
        costValue: 0,
        salesAmount: 0,
      };
      cur.salesQty += bucket.salesQty;
      cur.costValue += bucket.costValue;
      cur.salesAmount += bucket.salesAmount;
      target.set(id, cur);
    }
  }

  /** Returns adjust margin only; gross invoice qty (salesQty) is unchanged. */
  private mergeVendorMarginBuckets(
    target: Map<string, VendorBucket>,
    source: Map<string, VendorBucket>,
  ) {
    for (const [id, bucket] of source) {
      const cur = target.get(id) ?? {
        supplierId: id,
        salesQty: 0,
        costValue: 0,
        salesAmount: 0,
      };
      cur.costValue += bucket.costValue;
      cur.salesAmount += bucket.salesAmount;
      target.set(id, cur);
    }
  }

  private buildVendorRows(
    buckets: Map<string, VendorBucket>,
    vendorNameById: ReadonlyMap<string, string>,
  ): StoreVendorsSalesVendorRow[] {
    return [...buckets.values()]
      .filter((v) => v.salesQty > 0)
      .map((v) => {
        const totalCostValue = roundMoney(v.costValue);
        const totalSellingValue = roundMoney(v.salesAmount);
        const margin = roundMoney(totalSellingValue - totalCostValue);
        return {
          supplierId: v.supplierId,
          vendorName: vendorNameById.get(v.supplierId) ?? UNMAPPED_VENDOR_NAME,
          costPrice: v.salesQty > 0 ? roundMoney(totalCostValue / v.salesQty) : 0,
          sellingPrice: v.salesQty > 0 ? roundMoney(totalSellingValue / v.salesQty) : 0,
          salesQty: v.salesQty,
          totalCostValue,
          totalSellingValue,
          margin,
          marginPercent: totalCostValue > 0 ? roundMoney((margin / totalCostValue) * 100) : 0,
        };
      })
      .sort((a, b) => {
        if (a.supplierId === UNMAPPED_VENDOR_ID) return 1;
        if (b.supplierId === UNMAPPED_VENDOR_ID) return -1;
        return a.vendorName.localeCompare(b.vendorName);
      });
  }

  private buildAllVendorsSummary(
    buckets: Map<string, VendorBucket>,
    vendors: StoreVendorsSalesVendorRow[],
    counts: { invoiceCount: number; returnsCount: number; returnValue: number },
  ) {
    let grossSalesQty = 0;
    let mappedGrossQty = 0;
    let unmappedGrossQty = 0;
    for (const bucket of buckets.values()) {
      grossSalesQty += bucket.salesQty;
      if (bucket.supplierId === UNMAPPED_VENDOR_ID) {
        unmappedGrossQty += bucket.salesQty;
      } else {
        mappedGrossQty += bucket.salesQty;
      }
    }

    const salesQty = Math.max(0, grossSalesQty);
    const totalCostValue = roundMoney(vendors.reduce((s, v) => s + v.totalCostValue, 0));
    const totalSellingValue = roundMoney(vendors.reduce((s, v) => s + v.totalSellingValue, 0));
    const margin = roundMoney(totalSellingValue - totalCostValue);

    return {
      vendorCount: vendors.filter((v) => v.salesQty > 0).length,
      invoices: counts.invoiceCount,
      returnsCount: counts.returnsCount,
      salesQty,
      mappedSalesQty: Math.max(0, mappedGrossQty),
      unmappedSalesQty: Math.max(0, unmappedGrossQty),
      totalCostValue,
      totalSellingValue,
      margin,
      marginPercent: totalCostValue > 0 ? roundMoney((margin / totalCostValue) * 100) : 0,
      returnValue: roundMoney(counts.returnValue),
    };
  }

  private resolveStore(storeId?: string) {
    return resolveDashboardStore(this.storeModel, storeId);
  }

  private async resolveSupplier(supplierId: string) {
    const id = supplierId?.trim();
    if (!id || !Types.ObjectId.isValid(id)) {
      throw new BadRequestException('Invalid supplierId');
    }

    const supplier = await this.supplierModel.findById(id).lean();
    if (!supplier || supplier.isActive === false) {
      throw new NotFoundException(`Supplier '${id}' not found or inactive`);
    }

    return { id: String(supplier._id), name: supplier.name };
  }

  private async buildSupplierSkuIndex(supplierId: string) {
    const products = await this.productModel
      .find({ supplierNameId: new Types.ObjectId(supplierId) })
      .select('sku costPrice categoryId itemName')
      .lean();

    const skuSet = new Set<string>();
    const skuMeta = new Map<string, SupplierSkuMeta>();

    for (const p of products) {
      skuSet.add(p.sku);
      skuMeta.set(p.sku, {
        costPrice: p.costPrice ?? 0,
        categoryId: p.categoryId ? String(p.categoryId) : '',
        itemName: p.itemName ?? p.sku,
      });
    }

    return { skuSet, skuMeta };
  }

  private computeMarginSummary(marginLines: SignedMarginLine[], costBySku: Map<string, number>) {
    let totalCostValue = 0;
    let totalSellingValue = 0;
    for (const row of marginLines) {
      const totals = aggregateMarginLines([row], costBySku, row.sign);
      totalCostValue += totals.totalCostValue;
      totalSellingValue += totals.totalSellingValue;
    }
    const salesMargin = totalSellingValue - totalCostValue;
    return {
      totalCostValue: roundMoney(totalCostValue),
      totalSellingValue: roundMoney(totalSellingValue),
      salesMargin: roundMoney(salesMargin),
      marginPercentage: computeMarginPercentage(salesMargin, totalCostValue),
    };
  }

  private accumulateProduct(
    productTotals: Map<string, ProductAgg>,
    line: LineMarginRow,
    skuMeta: Map<string, SupplierSkuMeta>,
    costBySku: Map<string, number>,
    sign: 1 | -1,
  ) {
    const totals = aggregateMarginLines([line], costBySku, sign);
    const description = skuMeta.get(line.sku)?.itemName ?? line.sku;

    const cur = productTotals.get(line.sku) ?? {
      description,
      units: 0,
      salesAmount: 0,
      costValue: 0,
    };
    cur.units += sign * line.qty;
    cur.salesAmount += totals.totalSellingValue;
    cur.costValue += totals.totalCostValue;
    if (!cur.description) cur.description = description;
    productTotals.set(line.sku, cur);
  }

  private accumulateCategory(
    categoryPieces: Map<string, number>,
    sku: string,
    skuMeta: Map<string, SupplierSkuMeta>,
    qty: number,
  ) {
    const categoryId = skuMeta.get(sku)?.categoryId;
    if (!categoryId) return;
    categoryPieces.set(categoryId, (categoryPieces.get(categoryId) ?? 0) + qty);
  }

  private async buildCategoryMix(
    categoryPieces: Map<string, number>,
  ): Promise<StoreVendorSalesCategoryMixRow[]> {
    const categoryIds = [...categoryPieces.keys()].filter(Boolean);
    if (categoryIds.length === 0) return [];

    const categories = await this.categoryModel
      .find({ _id: { $in: categoryIds.map((id) => new Types.ObjectId(id)) } })
      .select('name')
      .lean();

    const nameById = new Map(categories.map((c) => [String(c._id), c.name]));

    const totalPieces = [...categoryPieces.values()].reduce((s, v) => s + v, 0);
    return [...categoryPieces.entries()]
      .map(([categoryId, pieces]) => ({
        categoryId,
        categoryName: nameById.get(categoryId) ?? 'Uncategorized',
        pieces,
        percent: totalPieces > 0 ? Math.round((pieces / totalPieces) * 100) : 0,
      }))
      .sort((a, b) => b.pieces - a.pieces);
  }

  private ensureBucket(map: Map<string, BucketAgg>, key: string) {
    if (!map.has(key)) {
      map.set(key, {
        invoices: 0,
        items: 0,
        gross: 0,
        net: 0,
        returnsCount: 0,
        returnValue: 0,
      });
    }
  }

  private buildSalesDetails(
    buckets: Map<string, BucketAgg>,
    bucketByMonth: boolean,
  ): StoreVendorSalesDetailRow[] {
    return [...buckets.entries()]
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([bucketKey, agg]) => ({
        bucketKey,
        label: labelForBucketKey(bucketKey, bucketByMonth),
        invoices: agg.invoices,
        items: agg.items,
        gross: roundMoney(agg.gross),
        net: roundMoney(agg.net - agg.returnValue),
        returnsCount: agg.returnsCount,
        returnValue: roundMoney(agg.returnValue),
      }));
  }

  private buildReturnBreakdown(
    buckets: Map<string, BucketAgg>,
    bucketByMonth: boolean,
  ): StoreVendorSalesReturnBreakdownRow[] {
    return [...buckets.entries()]
      .filter(([, agg]) => agg.returnsCount > 0)
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([bucketKey, agg]) => ({
        bucketKey,
        label: labelForBucketKey(bucketKey, bucketByMonth),
        returns: agg.returnsCount,
        returnValue: roundMoney(agg.returnValue),
      }));
  }

  private buildTopProducts(
    productTotals: Map<string, ProductAgg>,
    limit: number,
  ): StoreVendorSalesTopProductRow[] {
    const positive = [...productTotals.entries()].filter(([, v]) => v.units > 0);
    const totalUnits = positive.reduce((s, [, v]) => s + v.units, 0);

    return positive
      .map(([sku, v]) => ({
        sku,
        description: v.description,
        units: v.units,
        salesAmount: roundMoney(v.salesAmount),
        margin: roundMoney(v.salesAmount - v.costValue),
        percent: totalUnits > 0 ? Math.round((v.units / totalUnits) * 100) : 0,
      }))
      .sort((a, b) => b.units - a.units)
      .slice(0, limit);
  }

  private docTimestamp(doc: Record<string, unknown>): unknown {
    return doc.updatedAt ?? doc.createdAt;
  }
}
