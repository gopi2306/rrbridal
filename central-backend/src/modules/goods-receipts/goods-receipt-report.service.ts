import { Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model } from 'mongoose';
import {
  buildExportFilename,
  buildMultiSheetExcelBuffer,
  formatExportMarginPercent,
  formatExportMoney,
  type TabularExportSheet,
} from '../../common/tabular-export';
import { roundMoney } from '../../common/money.util';
import { isValidObjectIdString, toObjectId } from '../../common/object-id.util';
import {
  PurchaseOrder,
  PurchaseOrderDocument,
  PurchaseOrderLine,
} from '../purchase-orders/schemas/purchase-order.schema';
import { refreshPurchaseOrderLine } from '../purchase-orders/purchase-order-line-calculator';
import { Product, ProductDocument } from '../products/schemas/product.schema';
import { VendorReceiptReportQueryDto } from './dto/vendor-receipt-report-query.dto';
import { GoodsReceipt, GoodsReceiptDocument } from './schemas/goods-receipt.schema';

const VENDOR_WISE_HEADERS = [
  'Vendor',
  'Cost price (with tax)',
  'Selling price',
  'Qty',
  'Total cost value',
  'Total S.P. value',
  'Margin',
  'Margin %',
] as const;

const SUMMARY_HEADERS = ['Qty', 'Total cost value', 'Total S.P. value', 'Margin', 'Margin %'] as const;

type LinePricing = {
  netCost: number;
  selling: number;
};

type VendorBucket = {
  supplierId: string;
  vendorName: string;
  totalQty: number;
  totalCostValue: number;
  totalSellingValue: number;
};

export type VendorReceiptReportResult = {
  buffer: Buffer;
  contentType: string;
  filename: string;
};

function num(value: unknown, fallback = 0): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback;
}

function parseInclusiveDateRange(from?: string, to?: string): { from?: Date; to?: Date } {
  const result: { from?: Date; to?: Date } = {};
  if (from) {
    const d = new Date(`${from}T00:00:00.000Z`);
    if (!Number.isNaN(d.getTime())) result.from = d;
  }
  if (to) {
    const d = new Date(`${to}T23:59:59.999Z`);
    if (!Number.isNaN(d.getTime())) result.to = d;
  }
  return result;
}

function pricingFromProduct(product: {
  costPrice?: number;
  sellingPrice?: number;
  gstPercent?: number;
}): LinePricing {
  const snapshot: {
    costPrice?: number;
    sellingPrice?: number;
    gstPercent?: number;
  } = {};
  if (typeof product.costPrice === 'number') snapshot.costPrice = product.costPrice;
  if (typeof product.sellingPrice === 'number') snapshot.sellingPrice = product.sellingPrice;
  if (typeof product.gstPercent === 'number') snapshot.gstPercent = product.gstPercent;

  const refreshed = refreshPurchaseOrderLine(
    {
      sku: '',
      recdQty: 1,
      cost: num(product.costPrice, 0),
      taxPercent: num(product.gstPercent, 0),
    },
    snapshot,
  );
  return {
    netCost: num(refreshed.netCost, 0),
    selling: num(refreshed.selling, num(product.sellingPrice, 0)),
  };
}

function pricingFromPoLine(
  line: PurchaseOrderLine,
  product?: { costPrice?: number; sellingPrice?: number; gstPercent?: number },
): LinePricing {
  let netCost = num(line.netCost, 0);
  let selling = num(line.selling, 0);

  if (netCost <= 0 || selling <= 0) {
    const snapshot: {
      costPrice?: number;
      sellingPrice?: number;
      gstPercent?: number;
    } = {};
    if (product) {
      if (typeof product.costPrice === 'number') snapshot.costPrice = product.costPrice;
      if (typeof product.sellingPrice === 'number') snapshot.sellingPrice = product.sellingPrice;
      if (typeof product.gstPercent === 'number') snapshot.gstPercent = product.gstPercent;
    }

    const refreshed = refreshPurchaseOrderLine(
      {
        ...line,
        recdQty: Math.max(1, num(line.recdQty, 1)),
      },
      snapshot,
    );
    if (netCost <= 0) netCost = num(refreshed.netCost, 0);
    if (selling <= 0) selling = num(refreshed.selling, num(product?.sellingPrice, 0));
  }

  if (netCost <= 0 && product) {
    netCost = pricingFromProduct(product).netCost;
  }

  return { netCost, selling };
}

@Injectable()
export class GoodsReceiptReportService {
  constructor(
    @InjectModel(GoodsReceipt.name) private readonly grModel: Model<GoodsReceiptDocument>,
    @InjectModel(PurchaseOrder.name) private readonly poModel: Model<PurchaseOrderDocument>,
    @InjectModel(Product.name) private readonly productModel: Model<ProductDocument>,
  ) {}

  async buildVendorSummaryExport(query: VendorReceiptReportQueryDto): Promise<VendorReceiptReportResult> {
    const status = query.status ?? 'posted';
    const { from, to } = parseInclusiveDateRange(query.receiptDateFrom, query.receiptDateTo);

    const filter: FilterQuery<GoodsReceiptDocument> = { status };
    if (query.supplierId) filter['supplier.supplierId'] = query.supplierId;
    if (from || to) {
      filter.createdAt = {};
      if (from) filter.createdAt.$gte = from;
      if (to) filter.createdAt.$lte = to;
    }

    const receipts = await this.grModel.find(filter).lean();
    const poIds = [
      ...new Set(
        receipts
          .map((gr) => gr.poId?.trim())
          .filter((id): id is string => !!id && isValidObjectIdString(id)),
      ),
    ];
    const poNos = [
      ...new Set(
        receipts
          .map((gr) => gr.poNo?.trim())
          .filter((no): no is string => !!no),
      ),
    ];

    const poById = new Map<string, PurchaseOrderDocument>();
    const poByPoNo = new Map<string, PurchaseOrderDocument>();
    if (poIds.length > 0 || poNos.length > 0) {
      const orConditions: FilterQuery<PurchaseOrderDocument>[] = [];
      if (poIds.length > 0) {
        orConditions.push({ _id: { $in: poIds.map((id) => toObjectId(id)) } });
      }
      if (poNos.length > 0) {
        orConditions.push({ poNo: { $in: poNos } });
      }
      const pos = await this.poModel.find({ $or: orConditions }).lean();
      for (const po of pos) {
        poById.set(String(po._id), po as PurchaseOrderDocument);
        if (typeof po.poNo === 'string') poByPoNo.set(po.poNo, po as PurchaseOrderDocument);
      }
    }

    const allSkus = new Set<string>();
    for (const gr of receipts) {
      for (const line of gr.lines ?? []) {
        const sku = line.sku?.trim();
        if (sku) allSkus.add(sku);
      }
    }

    const productBySku = new Map<string, ProductDocument>();
    if (allSkus.size > 0) {
      const products = await this.productModel
        .find({ sku: { $in: [...allSkus] } })
        .select('sku costPrice sellingPrice gstPercent')
        .lean();
      for (const product of products) {
        if (typeof product.sku === 'string') productBySku.set(product.sku, product as ProductDocument);
      }
    }

    const vendors = new Map<string, VendorBucket>();

    for (const gr of receipts) {
      const supplierId = gr.supplier?.supplierId?.trim() || 'unknown';
      const vendorName = gr.supplier?.name?.trim() || supplierId;
      const po = this.resolvePurchaseOrder(gr, poById, poByPoNo);

      for (const line of gr.lines ?? []) {
        if ((line.outcome ?? 'valid') !== 'valid') continue;
        const qty = num(line.receivedQty, 0);
        if (qty <= 0) continue;

        const sku = line.sku?.trim();
        if (!sku) continue;

        const product = productBySku.get(sku);
        const poLine = this.findPoLine(po?.lines, line);
        const pricing = poLine
          ? pricingFromPoLine(poLine, product)
          : product
            ? pricingFromProduct(product)
            : { netCost: 0, selling: 0 };

        const costValue = roundMoney(pricing.netCost * qty);
        const sellingValue = roundMoney(pricing.selling * qty);

        const bucket = vendors.get(supplierId) ?? {
          supplierId,
          vendorName,
          totalQty: 0,
          totalCostValue: 0,
          totalSellingValue: 0,
        };
        bucket.vendorName = vendorName;
        bucket.totalQty += qty;
        bucket.totalCostValue = roundMoney(bucket.totalCostValue + costValue);
        bucket.totalSellingValue = roundMoney(bucket.totalSellingValue + sellingValue);
        vendors.set(supplierId, bucket);
      }
    }

    const vendorRows = [...vendors.values()]
      .sort((a, b) => a.vendorName.localeCompare(b.vendorName))
      .map((v) => {
        const margin = roundMoney(v.totalSellingValue - v.totalCostValue);
        const avgCost = v.totalQty > 0 ? v.totalCostValue / v.totalQty : 0;
        const avgSelling = v.totalQty > 0 ? v.totalSellingValue / v.totalQty : 0;
        return [
          v.vendorName,
          formatExportMoney(avgCost),
          formatExportMoney(avgSelling),
          String(v.totalQty),
          formatExportMoney(v.totalCostValue),
          formatExportMoney(v.totalSellingValue),
          formatExportMoney(margin),
          formatExportMarginPercent(margin, v.totalCostValue),
        ];
      });

    const grandQty = [...vendors.values()].reduce((sum, v) => sum + v.totalQty, 0);
    const grandCost = roundMoney([...vendors.values()].reduce((sum, v) => sum + v.totalCostValue, 0));
    const grandSelling = roundMoney([...vendors.values()].reduce((sum, v) => sum + v.totalSellingValue, 0));
    const grandMargin = roundMoney(grandSelling - grandCost);

    const summaryRow = [
      String(grandQty),
      formatExportMoney(grandCost),
      formatExportMoney(grandSelling),
      formatExportMoney(grandMargin),
      formatExportMarginPercent(grandMargin, grandCost),
    ];

    const sheets: TabularExportSheet[] = [
      { name: 'Vendor wise', headers: VENDOR_WISE_HEADERS, rows: vendorRows },
      { name: 'Summary', headers: SUMMARY_HEADERS, rows: [summaryRow] },
    ];

    const scopeCode = this.buildScopeCode(query);
    return {
      buffer: buildMultiSheetExcelBuffer(sheets),
      contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      filename: buildExportFilename('vendor-receipt-report', scopeCode, 'xlsx'),
    };
  }

  private resolvePurchaseOrder(
    gr: { poId?: string; poNo?: string },
    poById: Map<string, PurchaseOrderDocument>,
    poByPoNo: Map<string, PurchaseOrderDocument>,
  ): PurchaseOrderDocument | undefined {
    const poId = gr.poId?.trim();
    if (poId && poById.has(poId)) return poById.get(poId);
    const poNo = gr.poNo?.trim();
    if (poNo && poByPoNo.has(poNo)) return poByPoNo.get(poNo);
    return undefined;
  }

  private findPoLine(
    lines: PurchaseOrderLine[] | undefined,
    grLine: { sku?: string; productId?: unknown },
  ): PurchaseOrderLine | undefined {
    if (!lines?.length) return undefined;
    const sku = grLine.sku?.trim();
    const productId = grLine.productId != null ? String(grLine.productId) : '';
    if (productId) {
      const byProduct = lines.find((l) => l.productId != null && String(l.productId) === productId);
      if (byProduct) return byProduct;
    }
    if (sku) {
      return lines.find((l) => l.sku?.trim() === sku);
    }
    return undefined;
  }

  private buildScopeCode(query: VendorReceiptReportQueryDto): string {
    const from = query.receiptDateFrom ?? 'start';
    const to = query.receiptDateTo ?? 'end';
    const supplier = query.supplierId?.trim() ? query.supplierId.trim() : 'all-vendors';
    return `${from}-to-${to}-${supplier}`;
  }
}
