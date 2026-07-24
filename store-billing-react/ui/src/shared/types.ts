export type SessionUser = {
  id: string;
  email: string;
  name?: string;
  role: string;
  storeId: string;
  deviceId: string;
  posCounter: string;
  isPrimaryTill: boolean;
};

export type CatalogItem = {
  id: string;
  sku: string;
  name: string;
  barcode: string | null;
  category: string | null;
  brand: string | null;
  retailPrice: number;
  wholesalePrice: number;
  mrp: number;
  costPrice: number;
  stockQty: number;
  gstPercent: number;
  hsn: string | null;
  minStock: number;
  imageUrl: string | null;
};

export type CartLine = {
  key: string;
  sku: string;
  name: string;
  qty: number;
  rate: number;
  gstPercent: number;
  hsn?: string | null;
  stockQty: number;
};

export type BillingSettings = {
  storeId: string;
  mode: 'retail' | 'wholesale';
  allowPerBillSwitch: boolean;
};
