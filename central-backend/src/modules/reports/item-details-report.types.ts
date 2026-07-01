export type ItemDetailsReportFilters = {
  from?: string;
  to?: string;
  sku?: string;
  search?: string;
  storeId?: string;
  brandId?: string;
  supplierId?: string;
  limit: number;
  offset: number;
};

export type PurchasePoItemRow = {
  poNo: string;
  poDate: string;
  status: string;
  supplierId: string;
  supplierName: string;
  supplierCode?: string;
  branchId?: string;
  sku: string;
  productName: string;
  brandName?: string;
  orderedQty: number;
  cost: number;
  netCost: number;
  netAmount: number;
};

export type PurchaseGrnItemRow = {
  receiptNo: string;
  grnNumber?: string;
  poNo?: string;
  receiptDate: string;
  supplierId?: string;
  supplierName?: string;
  sku: string;
  productName: string;
  brandName?: string;
  orderedQty: number;
  receivedQty: number;
  outcome?: string;
};

export type SohItemRow = {
  sku: string;
  productName: string;
  brandName?: string;
  categoryName?: string;
  warehouseQty: number;
  inTransitQty: number;
  storeQty: number;
  totalSoh: number;
  salesQty: number;
  remainingQty: number;
  costPrice?: number;
  mrp?: number;
  sellingPrice?: number;
  storePrice?: number;
};

export type SalesItemRow = {
  storeId: string;
  invoiceNo: string;
  billDate: string;
  sku: string;
  productName: string;
  brandName?: string;
  qty: number;
  rate: number;
  amount: number;
  salesman?: string;
  salesmanCode?: string;
  paymentSummary?: string;
  isReturn: boolean;
  documentNo: string;
};

export type ItemDetailsReportSummary = {
  poLineCount: number;
  grnLineCount: number;
  sohSkuCount: number;
  salesLineCount: number;
  totalOrderedQty: number;
  totalReceivedQty: number;
  totalSohQty: number;
  totalSoldQty: number;
  totalSalesAmount: number;
  truncated: {
    poLines: boolean;
    grnLines: boolean;
    soh: boolean;
    sales: boolean;
  };
};

export type ItemDetailsReportResponse = {
  generatedAt: string;
  filters: ItemDetailsReportFilters;
  summary: ItemDetailsReportSummary;
  purchases: {
    poLines: PurchasePoItemRow[];
    grnLines: PurchaseGrnItemRow[];
  };
  soh: SohItemRow[];
  sales: SalesItemRow[];
};

export type ItemDetailsReportExportResult = {
  buffer: Buffer;
  contentType: string;
  filename: string;
};
