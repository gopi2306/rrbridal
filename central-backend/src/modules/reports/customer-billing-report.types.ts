export type CustomerBillingPaymentSplits = {
  cash: number;
  card: number;
  upi: number;
  creditNote: number;
};

export type CustomerBillingReturnDetail = {
  returnNo: string;
  returnDate: string;
  kind: string;
  returnMode: string;
  creditNoteNo: string;
  qty: number;
  amount: number;
};

export type CustomerBillingLineDetail = {
  lineNo: number;
  sku: string;
  description: string;
  hsn: string;
  qty: number;
  rate: number;
  amount: number;
  discountAmount: number;
  taxAmount: number;
};

export type CustomerBillingBillDetail = {
  billNo: string;
  billDate: string;
  posCounter: string;
  customerCode: string;
  customerName: string;
  customerPhone: string;
  qty: number;
  grossAmount: number;
  returnAmount: number;
  netAmount: number;
  payments: CustomerBillingPaymentSplits;
  returns: CustomerBillingReturnDetail[];
  lines: CustomerBillingLineDetail[];
};

export type CustomerBillingCustomerRow = {
  customerKey: string;
  customerCode: string;
  customerName: string;
  customerPhone: string;
  billCount: number;
  qty: number;
  grossAmount: number;
  returnAmount: number;
  netAmount: number;
  payments: CustomerBillingPaymentSplits;
  bills: CustomerBillingBillDetail[];
};

export type CustomerBillingTotals = {
  customerCount: number;
  billCount: number;
  qty: number;
  grossAmount: number;
  returnAmount: number;
  netAmount: number;
  payments: CustomerBillingPaymentSplits;
};

export type CustomerBillingReportResponse = {
  period: {
    from: string;
    to: string;
    timezone: string;
    storeCode: string;
    storeName: string;
    posCounter?: string;
  };
  filters: {
    customerSearch?: string;
    customerCode?: string;
    customerPhone?: string;
  };
  limit: number;
  truncated: boolean;
  total: number;
  totals: CustomerBillingTotals;
  data: CustomerBillingCustomerRow[];
};
