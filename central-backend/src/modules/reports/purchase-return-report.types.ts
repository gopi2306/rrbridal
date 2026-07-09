export type PurchaseReturnReportRow = {
  date: string;
  purchaseReturnNo: string;
  purchaseReturnNoNumeric: number;
  supplierName: string;
  itemName: string;
  qty: number;
  cgstAmount: number;
  sgstAmount: number;
  igstAmount: number;
  totalAmount: number;
  additionalDiscountPercent1: number;
  additionalDiscountPercent2: number;
  additionalDiscountPercent3: number;
  additionalDiscountAmount1: number;
  additionalDiscountAmount2: number;
  additionalDiscountAmount3: number;
  additionalEduCessPercent: number;
  bagQty: number;
  cessA: number;
  companyName: string;
  divisionName: string;
  eduCessPercent: number;
  exciseDutyPercent: number;
  freeQty: number;
  itemAlias: string;
  itemCode: string;
  loadUnloadCharge: number;
  locationName: string;
  mrp: number;
  observationAmount: number;
  purchaseNo: string;
  purchaseReturnReferenceNo: number;
  retailOutletId: string;
  slipNo: string;
  travelExpense: number;
};

export type PurchaseReturnReportTotals = {
  qty: number;
  cgstAmount: number;
  sgstAmount: number;
  igstAmount: number;
  totalAmount: number;
  mrp: number;
};

export type PurchaseReturnReportResponse = {
  period: {
    from: string;
    to: string;
    branchId?: string;
    mainDivisionId?: string;
    mainLocationId?: string;
    supplierId?: string;
    status?: string;
  };
  truncated: boolean;
  total: number;
  totals: PurchaseReturnReportTotals;
  data: PurchaseReturnReportRow[];
};
