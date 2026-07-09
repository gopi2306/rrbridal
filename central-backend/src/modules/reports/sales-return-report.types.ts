export type SalesReturnReportRow = {
  department: string;
  category: string;
  subCategory: string;
  brand: string;
  weightAndSize: string;
  weightPerGmOrMl: string;
  offerGroup: string;
  statusCategory: string;
  colour: string;
  returnDate: string;
  billNo: string;
  msrNo: string;
  customerName: string;
  itemName: string;
  qty: number;
  selling: number;
  mrp: number;
  taxPercent: number;
  taxAmount: number;
  returnAmount: number;
  returnCounter: string;
  billTime: string;
  returnTime: string;
  sku: string;
};

export type SalesReturnReportTotals = {
  qty: number;
  taxAmount: number;
  returnAmount: number;
};

export type SalesReturnReportResponse = {
  period: {
    from: string;
    to: string;
    storeCode?: string;
    storeName?: string;
    posCounter?: string;
  };
  truncated: boolean;
  total: number;
  totals: SalesReturnReportTotals;
  data: SalesReturnReportRow[];
};
