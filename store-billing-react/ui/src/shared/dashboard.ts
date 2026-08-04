import { api, getContext } from './api';

function storeId() {
  const c = getContext();
  if (!c?.storeId) throw new Error('Terminal context missing.');
  return c.storeId;
}

export function getStoreSales(period = 'today', from?: string, to?: string) {
  let url = `/api/dashboard/store/sales?storeId=${encodeURIComponent(storeId())}&period=${encodeURIComponent(period)}&billPage=1&billLimit=20`;
  if (from) url += `&from=${encodeURIComponent(from)}`;
  if (to) url += `&to=${encodeURIComponent(to)}`;
  return api<Record<string, unknown>>(url);
}

export function getStoreOps() {
  return api<Record<string, unknown>>(`/api/dashboard/store?storeId=${encodeURIComponent(storeId())}`);
}

export function getStoreDayClose(businessDate: string, posCounter?: string) {
  let url =
    `/api/dashboard/store/day-close?storeId=${encodeURIComponent(storeId())}` +
    `&businessDate=${encodeURIComponent(businessDate)}`;
  if (posCounter) url += `&posCounter=${encodeURIComponent(posCounter)}`;
  return api<Record<string, unknown>>(url);
}

export function getStoreDayCloseReport(businessDate: string, posCounter?: string) {
  let url =
    `/api/dashboard/store/day-close/report?storeId=${encodeURIComponent(storeId())}` +
    `&businessDate=${encodeURIComponent(businessDate)}`;
  if (posCounter) url += `&posCounter=${encodeURIComponent(posCounter)}`;
  return api<Record<string, unknown>>(url);
}

export function getSalesmenAnalytics(period = 'today') {
  return api(
    `/api/dashboard/store/sales/salesmen?storeId=${encodeURIComponent(storeId())}&period=${encodeURIComponent(period)}`,
  );
}

export function getBillMargin(period = 'today') {
  return api(
    `/api/dashboard/store/sales/bill-margin?storeId=${encodeURIComponent(storeId())}&period=${encodeURIComponent(period)}`,
  );
}

export function getVendorSales(period = 'today') {
  return api(
    `/api/dashboard/store/sales/vendors?storeId=${encodeURIComponent(storeId())}&period=${encodeURIComponent(period)}`,
  );
}

export function getInventoryGrid(search?: string, page = 1, limit = 100) {
  let url =
    `/api/inventory/grid?storeId=${encodeURIComponent(storeId())}&page=${page}&limit=${limit}`;
  if (search?.trim()) url += `&search=${encodeURIComponent(search.trim())}`;
  return api<Record<string, unknown>>(url);
}

export function postInventoryAdjustment(sku: string, qtyDelta: number, reason: string) {
  return api('/api/inventory-adjustments', {
    method: 'POST',
    body: JSON.stringify({
      locationKind: 'store',
      storeCode: storeId(),
      reason,
      lines: [{ sku, qtyDelta, note: reason }],
    }),
  });
}
