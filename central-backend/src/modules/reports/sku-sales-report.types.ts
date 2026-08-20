export const FAST_SELLERS_DEFAULT_LIMIT = 100;
export const UNKNOWN_SUPPLIER_NAME = 'Unknown supplier';

export type SkuSalesRow = {
  sku: string;
  description: string;
  supplierId: string;
  supplierName: string;
  soldQty: number;
  returnQty: number;
  netQty: number;
  soldAmount: number;
  returnAmount: number;
  netAmount: number;
};

export type SkuSalesTotals = {
  skuCount: number;
  soldQty: number;
  returnQty: number;
  netQty: number;
  soldAmount: number;
  returnAmount: number;
  netAmount: number;
};

export type SkuSalesPeriod = {
  from: string;
  to: string;
  timezone: string;
  storeCode: string;
  storeName: string;
  posCounter?: string;
};

export type SkuCatalogEntry = {
  sku: string;
  itemName?: string;
  supplierNameId?: string | null;
  supplierName?: string;
};

export type SupplierWiseSupplierRow = {
  supplierId: string;
  supplierName: string;
  productCount: number;
  soldQty: number;
  returnQty: number;
  netQty: number;
  soldAmount: number;
  returnAmount: number;
  netAmount: number;
  products: SkuSalesRow[];
};

export type SupplierWiseTotals = SkuSalesTotals & {
  supplierCount: number;
};

export type SupplierWiseReportResponse = {
  period: SkuSalesPeriod;
  filters: { search?: string };
  limit: number;
  truncated: boolean;
  total: number;
  totals: SupplierWiseTotals;
  data: SupplierWiseSupplierRow[];
};

export type FastSellerRow = SkuSalesRow & { rank: number };

export type FastSellersReportResponse = {
  period: SkuSalesPeriod;
  filters: { search?: string };
  limit: number;
  truncated: boolean;
  total: number;
  totals: SkuSalesTotals;
  data: FastSellerRow[];
};
