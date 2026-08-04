import { api, getContext } from './api';
import type { CatalogProduct } from './types';

function ctx() {
  const c = getContext();
  if (!c?.storeId) throw new Error('Terminal context missing. Sign in again.');
  return c;
}

function writeBody(payload: unknown) {
  const c = ctx();
  return { storeId: c.storeId, deviceId: c.deviceId, payload };
}

export async function searchCatalog(q: string, limit = 80): Promise<CatalogProduct[]> {
  const c = ctx();
  const rows = await api<Record<string, unknown>[]>(
    `/api/store-pos/catalog/search?storeCode=${encodeURIComponent(c.storeId)}&q=${encodeURIComponent(q)}&limit=${limit}`,
  );
  return (rows ?? []).map((row) => ({
    centralId: String(row.centralId ?? ''),
    sku: String(row.sku ?? ''),
    upcEanCode: row.upcEanCode ? String(row.upcEanCode) : undefined,
    name: String(row.name || row.sku || ''),
    shortName: row.shortName ? String(row.shortName) : undefined,
    alias: row.alias ? String(row.alias) : undefined,
    costPrice: Number(row.costPrice ?? 0),
    marginPercent: Number(row.marginPercent ?? 0),
    mrp: Number(row.mrp ?? 0),
    sellingPrice: Number(row.sellingPrice ?? 0),
    storePrice: Number(row.storePrice ?? row.sellingPrice ?? 0),
    gstPercent: Number(row.gstPercent ?? 0),
    hsnSac: row.hsnSac ? String(row.hsnSac) : undefined,
    stockQty: Number(row.stockQty ?? 0),
    categoryId: row.categoryId ? String(row.categoryId) : undefined,
    brandId: row.brandId ? String(row.brandId) : undefined,
    offerGroupId: row.offerGroupId ? String(row.offerGroupId) : undefined,
  })).filter((p) => p.sku);
}

export async function nextNumber(kind: string): Promise<string> {
  const c = ctx();
  const res = await api<{ value: string }>('/api/store-pos/next-number', {
    method: 'POST',
    body: JSON.stringify({
      storeId: c.storeId,
      deviceId: c.deviceId,
      posCounter: c.posCounter,
      kind,
    }),
  });
  if (!res?.value) throw new Error('Central next-number returned empty value.');
  return res.value;
}

export function postBill(payload: unknown) {
  return api('/api/store-pos/bills', { method: 'POST', body: JSON.stringify(writeBody(payload)) });
}

export function deleteBill(billNo: string, payload: unknown = {}) {
  return api(`/api/store-pos/bills/${encodeURIComponent(billNo)}`, {
    method: 'DELETE',
    body: JSON.stringify(writeBody(payload)),
  });
}

export function listBills(search?: string, limit = 50) {
  const c = ctx();
  let url = `/api/store-pos/bills?storeCode=${encodeURIComponent(c.storeId)}&limit=${limit}`;
  if (search?.trim()) url += `&search=${encodeURIComponent(search.trim())}`;
  return api<unknown[]>(url);
}

export function getBill(billNo: string) {
  const c = ctx();
  return api<Record<string, unknown>>(
    `/api/store-pos/bills/${encodeURIComponent(billNo)}?storeCode=${encodeURIComponent(c.storeId)}`,
  );
}

export function postCodPayment(billNo: string, payload: unknown) {
  return api(`/api/store-pos/bills/${encodeURIComponent(billNo)}/cod-payment`, {
    method: 'POST',
    body: JSON.stringify(writeBody(payload)),
  });
}

export function postCreditPayment(billNo: string, payload: unknown) {
  return api(`/api/store-pos/bills/${encodeURIComponent(billNo)}/credit-payment`, {
    method: 'POST',
    body: JSON.stringify(writeBody(payload)),
  });
}

export function postSaleReturn(payload: unknown, exchange = false) {
  const c = ctx();
  return api('/api/store-pos/sale-returns', {
    method: 'POST',
    body: JSON.stringify({ storeId: c.storeId, deviceId: c.deviceId, exchange, payload }),
  });
}

export function listSaleReturns(originalBillNo?: string, limit = 50) {
  const c = ctx();
  let url = `/api/store-pos/sale-returns?storeCode=${encodeURIComponent(c.storeId)}&limit=${limit}`;
  if (originalBillNo?.trim()) url += `&originalBillNo=${encodeURIComponent(originalBillNo.trim())}`;
  return api<unknown[]>(url);
}

export function postQuotation(payload: unknown) {
  return api('/api/store-pos/quotations', { method: 'POST', body: JSON.stringify(writeBody(payload)) });
}

export function convertQuotation(payload: unknown) {
  return api('/api/store-pos/quotations/convert', { method: 'POST', body: JSON.stringify(writeBody(payload)) });
}

export function cancelQuotation(payload: unknown) {
  return api('/api/store-pos/quotations/cancel', { method: 'POST', body: JSON.stringify(writeBody(payload)) });
}

export function listQuotations(status?: string, limit = 50) {
  const c = ctx();
  let url = `/api/store-pos/quotations?storeCode=${encodeURIComponent(c.storeId)}&limit=${limit}`;
  if (status?.trim()) url += `&status=${encodeURIComponent(status.trim())}`;
  return api<unknown[]>(url);
}

export function getQuotation(quotationNo: string) {
  const c = ctx();
  return api(
    `/api/store-pos/quotations/${encodeURIComponent(quotationNo)}?storeCode=${encodeURIComponent(c.storeId)}`,
  );
}

export function postCreditNote(payload: unknown) {
  return api('/api/store-pos/credit-notes', { method: 'POST', body: JSON.stringify(writeBody(payload)) });
}

export function applyCreditNote(payload: unknown) {
  return api('/api/store-pos/credit-notes/apply', { method: 'POST', body: JSON.stringify(writeBody(payload)) });
}

export function cashoutCreditNote(payload: unknown) {
  return api('/api/store-pos/credit-notes/cashout', { method: 'POST', body: JSON.stringify(writeBody(payload)) });
}

export function listCreditNotes(opts?: { customerPhone?: string; customerCode?: string; availableOnly?: boolean }) {
  const c = ctx();
  let url =
    `/api/store-pos/credit-notes?storeCode=${encodeURIComponent(c.storeId)}` +
    `&availableOnly=${opts?.availableOnly ? 'true' : 'false'}`;
  if (opts?.customerPhone) url += `&customerPhone=${encodeURIComponent(opts.customerPhone)}`;
  if (opts?.customerCode) url += `&customerCode=${encodeURIComponent(opts.customerCode)}`;
  return api<unknown[]>(url);
}

export function openDaySession(payload: unknown) {
  return api('/api/store-pos/day-sessions/open', { method: 'POST', body: JSON.stringify(writeBody(payload)) });
}

export function closeDaySession(payload: unknown) {
  return api('/api/store-pos/day-sessions/close', { method: 'POST', body: JSON.stringify(writeBody(payload)) });
}

export function getDaySession(businessDate: string) {
  const c = ctx();
  return api(
    `/api/store-pos/day-sessions/current?storeCode=${encodeURIComponent(c.storeId)}` +
      `&businessDate=${encodeURIComponent(businessDate)}` +
      `&posCounter=${encodeURIComponent(c.posCounter)}`,
  );
}

export function postCashMovement(payload: unknown) {
  return api('/api/store-pos/cash-movements', { method: 'POST', body: JSON.stringify(writeBody(payload)) });
}

export function listCashMovements(businessDate: string, limit = 100) {
  const c = ctx();
  return api(
    `/api/store-pos/cash-movements?storeCode=${encodeURIComponent(c.storeId)}` +
      `&businessDate=${encodeURIComponent(businessDate)}&limit=${limit}`,
  );
}

export function postDailyExpense(payload: unknown) {
  return api('/api/store-pos/daily-expenses', { method: 'POST', body: JSON.stringify(writeBody(payload)) });
}

export function listDailyExpenses(businessDate: string, limit = 100) {
  const c = ctx();
  return api(
    `/api/store-pos/daily-expenses?storeCode=${encodeURIComponent(c.storeId)}` +
      `&businessDate=${encodeURIComponent(businessDate)}&limit=${limit}`,
  );
}

export function listHeldBills() {
  const c = ctx();
  return api(
    `/api/store-pos/held-bills?storeCode=${encodeURIComponent(c.storeId)}&deviceId=${encodeURIComponent(c.deviceId)}`,
  );
}

export function upsertHeldBill(holdNo: string, payload: unknown) {
  const c = ctx();
  return api(`/api/store-pos/held-bills/${encodeURIComponent(holdNo)}`, {
    method: 'PUT',
    body: JSON.stringify({ storeId: c.storeId, deviceId: c.deviceId, payload }),
  });
}

export function deleteHeldBill(holdNo: string) {
  const c = ctx();
  return api(
    `/api/store-pos/held-bills/${encodeURIComponent(holdNo)}?storeCode=${encodeURIComponent(c.storeId)}`,
    { method: 'DELETE' },
  );
}

export function postAdjustmentBill(payload: unknown) {
  return api('/api/store-pos/adjustment-bills', { method: 'POST', body: JSON.stringify(writeBody(payload)) });
}

export function listAdjustments(originalBillNo?: string, limit = 100) {
  const c = ctx();
  let url = `/api/store-pos/adjustment-bills?storeCode=${encodeURIComponent(c.storeId)}&limit=${limit}`;
  if (originalBillNo?.trim()) url += `&originalBillNo=${encodeURIComponent(originalBillNo.trim())}`;
  return api<unknown[]>(url);
}

export function listGatewayPayments(limit = 200, posCounter?: string) {
  const c = ctx();
  let url = `/api/store-pos/gateway-payments?storeCode=${encodeURIComponent(c.storeId)}&limit=${limit}`;
  if (posCounter) url += `&posCounter=${encodeURIComponent(posCounter)}`;
  return api<unknown[]>(url);
}

export function listActivePromotions() {
  const c = ctx();
  return api(`/api/store-pos/promotions?storeCode=${encodeURIComponent(c.storeId)}`);
}

export function syncHealth() {
  return api('/api/sync/health', { auth: false });
}

export function getStore(code: string) {
  return api<Record<string, unknown>>(`/api/stores/${encodeURIComponent(code)}`);
}

export function getBarcodeLabelDesign() {
  return api('/api/barcode-label-designs/active');
}

export function listCustomers(q?: string) {
  const url = q?.trim()
    ? `/api/customers?search=${encodeURIComponent(q.trim())}`
    : '/api/customers';
  return api<unknown[]>(url);
}

export function createCustomer(body: unknown) {
  return api('/api/customers', { method: 'POST', body: JSON.stringify(body) });
}

export function patchCustomer(id: string, body: unknown) {
  return api(`/api/customers/${encodeURIComponent(id)}`, { method: 'PATCH', body: JSON.stringify(body) });
}

export function listSalesmen() {
  const c = ctx();
  return api<unknown[]>(`/api/salesmen?storeId=${encodeURIComponent(c.storeId)}`);
}

export function createSalesman(body: unknown) {
  return api('/api/salesmen', { method: 'POST', body: JSON.stringify(body) });
}

export function patchSalesman(id: string, body: unknown) {
  return api(`/api/salesmen/${encodeURIComponent(id)}`, { method: 'PATCH', body: JSON.stringify(body) });
}
