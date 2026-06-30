import type { BillListPaymentModeKey, BillListStatusKey } from '../dashboard/store-sales-payload.util';

export interface BillListRow {
  billNo: string;
  occurredAt: string;
  storeCode: string;
  storeName: string;
  customerName: string | null;
  salesmanCode: string | null;
  salesmanId: string | null;
  salesmanName: string | null;
  itemCount: number;
  netAmount: number;
  paymentMode: string;
  status: string;
  statusKey: BillListStatusKey;
  paymentModeKey: BillListPaymentModeKey;
}

export interface BillsListPage {
  storeCode: string;
  storeName: string;
  from: string;
  to: string;
  data: BillListRow[];
  total: number;
  page: number;
  limit: number;
  totalPages: number;
}

export interface BillDetailLine {
  lineNo: number;
  sku: string;
  description: string;
  hsn: string | null;
  qty: number;
  rate: number;
  amount: number;
  mrp: number;
  taxPercent: number;
  taxAmount: number;
  cgstPercent: number;
  sgstPercent: number;
  igstPercent: number;
  alterationAmount: number;
  discountAmount: number;
  cashDiscountAmount: number;
  schemeDiscountAmount: number;
  revisedAmount: number;
  revisedTaxAmount: number;
}

export interface BillDetailPayment {
  provider: string;
  amount: number;
  reference: string | null;
  status: string | null;
}

export interface BillDetailLinkedReturn {
  returnNo: string;
  mode: string | null;
  creditNoteNo: string | null;
}

export interface BillDetailLinkedAdjustment {
  adjustmentNo: string;
}

export interface BillDetailResponse {
  billNo: string;
  billDate: string | null;
  occurredAt: string;
  storeCode: string;
  storeName: string;
  posCounter: string | null;
  customerCode: string | null;
  customerName: string | null;
  customerPhone: string | null;
  salesman: string | null;
  salesmanCode: string | null;
  salesmanId: string | null;
  holdBills: boolean;
  doorDelivery: boolean;
  onlineCod: boolean;
  stitching: boolean;
  isInterState: boolean;
  deliveryDate: string | null;
  printInvoice: boolean;
  status: string;
  statusKey: BillListStatusKey;
  paymentMode: string;
  paymentModeKey: BillListPaymentModeKey;
  totals: {
    subTotal: number;
    itemDiscount: number;
    cashDiscount: number;
    schemeDiscount: number;
    roundOff: number;
    payable: number;
    originalTaxTotal: number;
    revisedSubTotal: number;
    cgstTotal: number;
    sgstTotal: number;
    igstTotal: number;
    creditApplied: number;
  };
  lines: BillDetailLine[];
  payments: BillDetailPayment[];
  linkedReturn: BillDetailLinkedReturn | null;
  linkedAdjustment: BillDetailLinkedAdjustment | null;
}

export interface BillsListOptions {
  storeCode?: string | undefined;
  search?: string | undefined;
  from?: string | undefined;
  to?: string | undefined;
  page: number;
  limit: number;
  status?: BillListStatusKey | undefined;
  paymentMode?: BillListPaymentModeKey | undefined;
  salesmanCode?: string | undefined;
}
