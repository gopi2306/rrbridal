export type StoreDayCloseDashboardOptions = {
  storeId?: string;
  businessDate: string;
  posCounter?: string;
};

export type StoreDayCloseCounterRow = {
  posCounter: string;
  status: string;
  openingCash: number;
  expectedCash: number;
  actualCashCounted: number;
  cashDifference: number;
  closedBy?: string;
  closedAtUtc?: string;
  payload?: Record<string, unknown>;
};

export type StoreDayCloseDashboardResponse = {
  storeId: string;
  businessDate: string;
  counters: StoreDayCloseCounterRow[];
  totals: {
    openingCash: number;
    expectedCash: number;
    actualCashCounted: number;
    cashDifference: number;
  };
};
