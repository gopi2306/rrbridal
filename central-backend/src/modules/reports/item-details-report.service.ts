import { Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { enrichProductDocuments } from '../../common/product-line-enrichment';
import { roundMoney } from '../../common/money.util';
import {
  parseOccurredAt,
  readString,
} from '../dashboard/store-sales-payload.util';
import {
  GoodsReceipt,
  GoodsReceiptDocument,
} from '../goods-receipts/schemas/goods-receipt.schema';
import { InventoryService } from '../inventory/inventory.service';
import { readPopulatedName } from '../inventory/export/inventory-export-columns';
import {
  PurchaseOrder,
  PurchaseOrderDocument,
} from '../purchase-orders/schemas/purchase-order.schema';
import { Product, ProductDocument } from '../products/schemas/product.schema';
import { StoreInvoice, StoreInvoiceDocument } from '../store-sales/schemas/store-invoice.schema';
import {
  StoreSaleReturn,
  StoreSaleReturnDocument,
} from '../store-sales/schemas/store-sale-return.schema';
import {
  buildItemDetailsSummary,
  formatReportDate,
  isDateInBounds,
  paginateRows,
  parseDocumentDate,
  parseInclusiveDateBounds,
  parseSalesLinesFromPayload,
  passesProductFilters,
  productInfoFromEnriched,
  normalizeReportFilters,
  aggregateSalesQtyBySku,
  enrichSohRowsWithSalesQty,
  type ProductRefInfo,
} from './item-details-report.helpers';
import type { ItemDetailsReportQueryDto } from './dto/item-details-report-query.dto';
import type {
  ItemDetailsReportFilters,
  ItemDetailsReportResponse,
  PurchaseGrnItemRow,
  PurchasePoItemRow,
  SalesItemRow,
  SohItemRow,
} from './item-details-report.types';

function readDocCreatedAt(doc: Record<string, unknown>): Date | undefined {
  const createdAt = doc.createdAt;
  return createdAt instanceof Date ? createdAt : undefined;
}

function num(value: unknown, fallback = 0): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback;
}

function enrichSalesRow(line: SalesItemRow, product: ProductRefInfo): SalesItemRow {
  const row: SalesItemRow = {
    ...line,
    productName: product.itemName || line.productName,
  };
  if (product.brandName) row.brandName = product.brandName;
  return row;
}

@Injectable()
export class ItemDetailsReportService {
  constructor(
    @InjectModel(PurchaseOrder.name) private readonly poModel: Model<PurchaseOrderDocument>,
    @InjectModel(GoodsReceipt.name) private readonly grnModel: Model<GoodsReceiptDocument>,
    @InjectModel(StoreInvoice.name) private readonly invoiceModel: Model<StoreInvoiceDocument>,
    @InjectModel(StoreSaleReturn.name) private readonly returnModel: Model<StoreSaleReturnDocument>,
    @InjectModel(Product.name) private readonly productModel: Model<ProductDocument>,
    private readonly inventoryService: InventoryService,
  ) {}

  async buildReport(query: ItemDetailsReportQueryDto): Promise<ItemDetailsReportResponse> {
    const filters = normalizeReportFilters(query);
    const productLookup = await this.buildProductLookup();

    const [allPoLines, allGrnLines, allSohRows, allSalesRows] = await Promise.all([
      this.buildPoLines(filters, productLookup),
      this.buildGrnLines(filters, productLookup),
      this.buildSohRows(filters, productLookup),
      this.buildSalesRows(filters, productLookup),
    ]);

    enrichSohRowsWithSalesQty(allSohRows, aggregateSalesQtyBySku(allSalesRows));

    const poPaged = paginateRows(allPoLines, filters.limit, filters.offset);
    const grnPaged = paginateRows(allGrnLines, filters.limit, filters.offset);
    const sohPaged = paginateRows(allSohRows, filters.limit, filters.offset);
    const salesPaged = paginateRows(allSalesRows, filters.limit, filters.offset);

    const truncated = {
      poLines: poPaged.truncated,
      grnLines: grnPaged.truncated,
      soh: sohPaged.truncated,
      sales: salesPaged.truncated,
    };

    const summary = buildItemDetailsSummary(
      poPaged.rows,
      grnPaged.rows,
      sohPaged.rows,
      salesPaged.rows,
      truncated,
      {
        poLineCount: allPoLines.length,
        grnLineCount: allGrnLines.length,
        sohSkuCount: allSohRows.length,
        salesLineCount: allSalesRows.length,
      },
    );

    return {
      generatedAt: new Date().toISOString(),
      filters,
      summary,
      purchases: {
        poLines: poPaged.rows,
        grnLines: grnPaged.rows,
      },
      soh: sohPaged.rows,
      sales: salesPaged.rows,
    };
  }

  async buildFullReportForExport(
    query: ItemDetailsReportQueryDto,
    maxRows: number,
  ): Promise<ItemDetailsReportResponse> {
    return this.buildReport({
      ...query,
      offset: 0,
      limit: maxRows,
    });
  }

  private async buildProductLookup(): Promise<Map<string, ProductRefInfo>> {
    const docs = await this.productModel.find({}).lean();
    const enriched = await enrichProductDocuments(
      this.productModel,
      docs as Array<Record<string, unknown>>,
    );
    const map = new Map<string, ProductRefInfo>();
    for (const doc of enriched) {
      const info = productInfoFromEnriched(doc);
      if (info.sku) map.set(info.sku.toLowerCase(), info);
    }
    return map;
  }

  private resolveProduct(
    lookup: Map<string, ProductRefInfo>,
    sku: string,
    description?: string,
  ): ProductRefInfo {
    const found = lookup.get(sku.toLowerCase());
    if (found) return found;
    return { sku, itemName: description?.trim() || sku };
  }

  private async buildPoLines(
    filters: ItemDetailsReportFilters,
    productLookup: Map<string, ProductRefInfo>,
  ): Promise<PurchasePoItemRow[]> {
    const bounds = parseInclusiveDateBounds(filters.from, filters.to);
    const docs = await this.poModel.find({}).lean();
    const rows: PurchasePoItemRow[] = [];

    for (const doc of docs) {
      const docRecord = doc as Record<string, unknown>;
      const poDate = parseDocumentDate(doc.poDate, readDocCreatedAt(docRecord));
      if (!isDateInBounds(poDate, bounds)) continue;

      const supplier = doc.supplier;
      const supplierId = supplier?.supplierId ?? '';
      const supplierName = supplier?.name ?? supplier?.shortname ?? '';
      const supplierCode = supplier?.code;

      for (const line of doc.lines ?? []) {
        const sku = (line.sku ?? '').trim();
        if (!sku) continue;
        const product = this.resolveProduct(productLookup, sku, line.description);
        if (!passesProductFilters(sku, line.description ?? '', product, filters)) continue;

        const poRow: PurchasePoItemRow = {
          poNo: doc.poNo,
          poDate: formatReportDate(poDate),
          status: doc.status,
          supplierId,
          supplierName,
          sku,
          productName: product.itemName || line.description || sku,
          orderedQty: num(line.recdQty),
          cost: roundMoney(num(line.cost)),
          netCost: roundMoney(num(line.netCost)),
          netAmount: roundMoney(num(line.netAmount)),
        };
        if (supplierCode) poRow.supplierCode = supplierCode;
        if (doc.branchId) poRow.branchId = doc.branchId;
        if (product.brandName) poRow.brandName = product.brandName;
        rows.push(poRow);
      }
    }

    return rows.sort((a, b) => a.poDate.localeCompare(b.poDate) || a.sku.localeCompare(b.sku));
  }

  private async buildGrnLines(
    filters: ItemDetailsReportFilters,
    productLookup: Map<string, ProductRefInfo>,
  ): Promise<PurchaseGrnItemRow[]> {
    const bounds = parseInclusiveDateBounds(filters.from, filters.to);
    const docs = await this.grnModel.find({ status: 'posted' }).lean();
    const rows: PurchaseGrnItemRow[] = [];

    for (const doc of docs) {
      const docRecord = doc as Record<string, unknown>;
      const receiptDate = parseDocumentDate(readDocCreatedAt(docRecord));
      if (!isDateInBounds(receiptDate, bounds)) continue;

      const supplier = doc.supplier;
      for (const line of doc.lines ?? []) {
        const sku = (line.sku ?? '').trim();
        if (!sku) continue;
        const product = this.resolveProduct(productLookup, sku, line.description);
        if (!passesProductFilters(sku, line.description ?? '', product, filters)) continue;

        const grnRow: PurchaseGrnItemRow = {
          receiptNo: doc.receiptNo,
          receiptDate: formatReportDate(receiptDate),
          sku,
          productName: product.itemName || line.description || sku,
          orderedQty: num(line.orderedQty),
          receivedQty: num(line.receivedQty),
        };
        if (doc.grnNumber) grnRow.grnNumber = doc.grnNumber;
        if (doc.poNo) grnRow.poNo = doc.poNo;
        if (supplier?.supplierId) grnRow.supplierId = supplier.supplierId;
        if (supplier?.name) grnRow.supplierName = supplier.name;
        if (product.brandName) grnRow.brandName = product.brandName;
        if (line.outcome) grnRow.outcome = line.outcome;
        rows.push(grnRow);
      }
    }

    return rows.sort((a, b) => a.receiptDate.localeCompare(b.receiptDate) || a.sku.localeCompare(b.sku));
  }

  private async buildSohRows(
    filters: ItemDetailsReportFilters,
    productLookup: Map<string, ProductRefInfo>,
  ): Promise<SohItemRow[]> {
    const fetchParams: { search?: string; storeId?: string; maxRows?: number } = {
      maxRows: 10_000,
    };
    if (filters.search) fetchParams.search = filters.search;
    if (filters.storeId) fetchParams.storeId = filters.storeId;

    const gridRows = await this.inventoryService.fetchAllWarehouseStoreRows(fetchParams);
    const rows: SohItemRow[] = [];

    for (const row of gridRows) {
      const sku = row.sku;
      const product = this.resolveProduct(
        productLookup,
        sku,
        typeof row.product.itemName === 'string' ? row.product.itemName : undefined,
      );
      if (!passesProductFilters(sku, product.itemName, product, filters)) continue;

      const warehouseQty = row.warehouseQty;
      const inTransitQty = row.inTransitQty;
      const storeQty = row.storeQty;
      const totalSoh = roundMoney(warehouseQty + inTransitQty + storeQty);
      const brandName = product.brandName || readPopulatedName(row.product.brandId);
      const categoryName = readPopulatedName(row.product.categoryId);

      const sohRow: SohItemRow = {
        sku,
        productName: product.itemName,
        warehouseQty,
        inTransitQty,
        storeQty,
        totalSoh,
        salesQty: 0,
        remainingQty: totalSoh,
      };
      if (brandName) sohRow.brandName = brandName;
      if (categoryName) sohRow.categoryName = categoryName;
      if (typeof row.product.costPrice === 'number') sohRow.costPrice = row.product.costPrice;
      if (typeof row.mrp === 'number') sohRow.mrp = row.mrp;
      if (typeof row.product.sellingPrice === 'number') sohRow.sellingPrice = row.product.sellingPrice;
      if (typeof row.storePrice === 'number') sohRow.storePrice = row.storePrice;
      rows.push(sohRow);
    }

    return rows.sort((a, b) => a.sku.localeCompare(b.sku));
  }

  private async buildSalesRows(
    filters: ItemDetailsReportFilters,
    productLookup: Map<string, ProductRefInfo>,
  ): Promise<SalesItemRow[]> {
    const bounds = parseInclusiveDateBounds(filters.from, filters.to);
    const rows: SalesItemRow[] = [];

    const invoiceFilter = filters.storeId ? { storeId: filters.storeId } : {};
    const invoiceCursor = this.invoiceModel
      .find(invoiceFilter)
      .select({ storeId: 1, invoiceNo: 1, payload: 1, createdAt: 1 })
      .lean()
      .cursor();

    for await (const doc of invoiceCursor) {
      const docRecord = doc as Record<string, unknown>;
      const payload = (doc.payload ?? {}) as Record<string, unknown>;
      const occurred = parseOccurredAt(payload, readDocCreatedAt(docRecord));
      if (!isDateInBounds(occurred, bounds)) continue;

      const parsed = parseSalesLinesFromPayload(payload, {
        storeId: doc.storeId,
        documentNo: doc.invoiceNo,
        invoiceNo: doc.invoiceNo,
        docCreatedAt: readDocCreatedAt(docRecord),
        isReturn: false,
      });

      for (const line of parsed) {
        const product = this.resolveProduct(productLookup, line.sku, line.productName);
        if (!passesProductFilters(line.sku, line.productName, product, filters)) continue;
        rows.push(enrichSalesRow(line, product));
      }
    }

    const returnFilter = filters.storeId ? { storeId: filters.storeId } : {};
    const returnCursor = this.returnModel
      .find(returnFilter)
      .select({ storeId: 1, returnNo: 1, kind: 1, payload: 1, createdAt: 1 })
      .lean()
      .cursor();

    for await (const doc of returnCursor) {
      const docRecord = doc as Record<string, unknown>;
      const payload = (doc.payload ?? {}) as Record<string, unknown>;
      const occurred = parseOccurredAt(payload, readDocCreatedAt(docRecord));
      if (!isDateInBounds(occurred, bounds)) continue;

      const returnCtx: {
        storeId: string;
        documentNo: string;
        invoiceNo?: string;
        docCreatedAt?: unknown;
        isReturn?: boolean;
      } = {
        storeId: doc.storeId,
        documentNo: doc.returnNo,
        docCreatedAt: readDocCreatedAt(docRecord),
        isReturn: true,
      };
      const linkedInvoice = readString(payload.billNo) ?? readString(payload.originalBillNo);
      if (linkedInvoice) returnCtx.invoiceNo = linkedInvoice;

      const parsed = parseSalesLinesFromPayload(payload, returnCtx);

      for (const line of parsed) {
        const product = this.resolveProduct(productLookup, line.sku, line.productName);
        if (!passesProductFilters(line.sku, line.productName, product, filters)) continue;
        rows.push(enrichSalesRow(line, product));
      }
    }

    return rows.sort((a, b) => a.billDate.localeCompare(b.billDate) || a.sku.localeCompare(b.sku));
  }
}
