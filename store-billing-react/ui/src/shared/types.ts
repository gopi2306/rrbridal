export type CatalogProduct = {
  centralId?: string;
  sku: string;
  upcEanCode?: string;
  name: string;
  shortName?: string;
  alias?: string;
  costPrice?: number;
  marginPercent?: number;
  mrp?: number;
  sellingPrice?: number;
  storePrice?: number;
  gstPercent?: number;
  hsnSac?: string;
  stockQty?: number;
  categoryId?: string;
  brandId?: string;
  offerGroupId?: string;
};

export type BillLine = {
  lineId: string;
  sku: string;
  name: string;
  qty: number;
  mrp: number;
  rate: number;
  discountPercent: number;
  gstPercent: number;
  hsnSac?: string;
  amount: number;
};

export type SalesmanRow = {
  _id?: string;
  id?: string;
  code?: string;
  name?: string;
  phone?: string;
  isActive?: boolean;
  displayLabel?: string;
};

export type CustomerRow = {
  _id?: string;
  id?: string;
  code?: string;
  name?: string;
  phone?: string;
  isCreditCustomer?: boolean;
};

/** Screens restricted to counter 1 by default (matches WPF CounterScreenAccess defaults). */
export const PRIMARY_ONLY_SCREENS = new Set([
  'dashboard',
  'analytics',
  'online-sales',
  'credit-bills',
  'ledger',
  'expenses',
  'settings',
]);
