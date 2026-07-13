import { roundMoney } from '../../common/money.util';
import {
  UNMAPPED_VENDOR_ID,
} from './store-vendors-sales-dashboard.types';
import type { LineAgg, LineMarginRow } from './store-sales-payload.util';

export type VendorSalesBucket = {
  supplierId: string;
  salesQty: number;
  costValue: number;
  salesAmount: number;
};

export type SaleLineAttributionResult = {
  byVendor: Map<string, VendorSalesBucket>;
  totalQty: number;
  mappedQty: number;
  unmappedQty: number;
  costValue: number;
  salesAmount: number;
};

export function buildGlobalSkuVendorIndex(
  products: ReadonlyArray<{
    sku: string;
    costPrice?: number;
    supplierNameId?: { toString(): string } | string | null;
  }>,
): { skuVendorMap: Map<string, string>; costBySku: Map<string, number> } {
  const skuVendorMap = new Map<string, string>();
  const costBySku = new Map<string, number>();

  for (const p of products) {
    const supplierId = p.supplierNameId ? String(p.supplierNameId) : UNMAPPED_VENDOR_ID;
    skuVendorMap.set(p.sku, supplierId);
    costBySku.set(p.sku, p.costPrice ?? 0);
  }

  return { skuVendorMap, costBySku };
}

export function attributeSaleLines(
  qtyLines: readonly LineAgg[],
  marginLines: readonly LineMarginRow[],
  skuVendorMap: ReadonlyMap<string, string>,
  costBySku: ReadonlyMap<string, number>,
  sign: 1 | -1,
): SaleLineAttributionResult {
  const marginBySku = new Map(marginLines.map((l) => [l.sku, l]));
  const byVendor = new Map<string, VendorSalesBucket>();
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
    const costUnit = margin?.costPerUnit ? margin.costPerUnit : (costBySku.get(line.sku) ?? 0);
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

export function mergeVendorSalesBuckets(
  target: Map<string, VendorSalesBucket>,
  source: Map<string, VendorSalesBucket>,
): void {
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
export function mergeVendorMarginBuckets(
  target: Map<string, VendorSalesBucket>,
  source: Map<string, VendorSalesBucket>,
): void {
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

export type SkuSalesBucket = {
  sku: string;
  salesQty: number;
  costValue: number;
  salesAmount: number;
};

export function attributeSaleLinesBySku(
  qtyLines: readonly LineAgg[],
  marginLines: readonly LineMarginRow[],
  costBySku: ReadonlyMap<string, number>,
  sign: 1 | -1,
): Map<string, SkuSalesBucket> {
  const marginBySku = new Map(marginLines.map((l) => [l.sku, l]));
  const bySku = new Map<string, SkuSalesBucket>();

  for (const line of qtyLines) {
    if (line.qty <= 0) continue;

    const margin = marginBySku.get(line.sku);
    const qty = sign * line.qty;
    const costUnit = margin?.costPerUnit ? margin.costPerUnit : (costBySku.get(line.sku) ?? 0);
    const lineCost = sign * costUnit * line.qty;
    const lineSell = margin ? sign * margin.sellingValue : 0;

    const bucket = bySku.get(line.sku) ?? {
      sku: line.sku,
      salesQty: 0,
      costValue: 0,
      salesAmount: 0,
    };
    bucket.salesQty += qty;
    bucket.costValue += lineCost;
    bucket.salesAmount += lineSell;
    bySku.set(line.sku, bucket);
  }

  return bySku;
}

export function mergeSkuSalesBuckets(
  target: Map<string, SkuSalesBucket>,
  source: Map<string, SkuSalesBucket>,
): void {
  for (const [sku, bucket] of source) {
    const cur = target.get(sku) ?? {
      sku,
      salesQty: 0,
      costValue: 0,
      salesAmount: 0,
    };
    cur.salesQty += bucket.salesQty;
    cur.costValue += bucket.costValue;
    cur.salesAmount += bucket.salesAmount;
    target.set(sku, cur);
  }
}

export function computeMarginPercent(margin: number, costValue: number): number {
  return costValue > 0 ? roundMoney((margin / costValue) * 100) : 0;
}
