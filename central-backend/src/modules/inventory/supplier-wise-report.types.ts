import type {
  SupplierWiseReportScope,
} from './dto/supplier-wise-report-query.dto';

export type SupplierWiseReportStoreContext = {
  code: string;
  name: string;
  label: string;
};

export type SupplierWiseReportFilters = {
  scope: SupplierWiseReportScope;
  store: SupplierWiseReportStoreContext;
  search?: string;
  brandId?: string;
  categoryId?: string;
  supplierId?: string;
};

export type SupplierWiseReportSummary = {
  supplierCount: number;
  productCount: number;
  stockQty: number;
  totalCostValue: number;
  totalSellingValue: number;
  totalMargin: number;
  marginPercent: number;
};

export type SupplierWiseReportRow = {
  supplierId: string;
  supplierName: string;
  stockQty: number;
  productCount: number;
  costValue: number;
  sellingValue: number;
  margin: number;
  marginPercent: number;
};

export type SupplierWiseReportResponse = {
  filters: SupplierWiseReportFilters;
  summary: SupplierWiseReportSummary;
  rows: SupplierWiseReportRow[];
};

export type SupplierWiseProductReportSummary = {
  productCount: number;
  stockQty: number;
  costValue: number;
  sellingValue: number;
  margin: number;
  marginPercent: number;
};

export type SupplierWiseProductReportRow = {
  sku: string;
  productName: string;
  stockQty: number;
  costValue: number;
  sellingValue: number;
  margin: number;
  marginPercent: number;
};

export type SupplierWiseProductReportResponse = {
  supplier: { id: string; name: string };
  filters: SupplierWiseReportFilters;
  summary: SupplierWiseProductReportSummary;
  rows: SupplierWiseProductReportRow[];
};

export type SupplierWiseReportExportResult = {
  buffer: Buffer;
  contentType: string;
  filename: string;
};

export type SupplierWiseReportOptions = {
  scope: SupplierWiseReportScope;
  storeId?: string;
  search?: string;
  brandId?: string;
  categoryId?: string;
  supplierId?: string;
};
