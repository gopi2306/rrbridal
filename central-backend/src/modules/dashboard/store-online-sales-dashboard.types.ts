export type StoreOnlineSalesDashboardOptions = {
  storeId?: string;
  period: 'today' | 'week' | 'month' | 'year' | 'custom';
  from?: string;
  to?: string;
  year: number;
  month: number;
  status?: 'all' | 'pending' | 'received';
};

export type StoreOnlineSalesSummary = {
  balanceTill: number;
  pendingCount: number;
  pendingAmount: number;
  receivedCount: number;
  receivedAmount: number;
  totalOnlineOrders: number;
};

export type StoreOnlineSalesItemRow = {
  billNo: string;
  customerName: string;
  customerPhone: string;
  amount: number;
  status: string;
  transactionNo?: string;
  receivedPaymentMode?: string;
  occurredAt: string;
};

export type StoreOnlineSalesDashboardResponse = {
  store: { code: string; name: string };
  period: { label: string; from: string; to: string };
  summary: StoreOnlineSalesSummary;
  items: StoreOnlineSalesItemRow[];
};
