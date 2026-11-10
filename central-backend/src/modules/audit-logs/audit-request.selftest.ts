/**
 * Self-test: mutation audit mapping (no Nest bootstrap).
 * Run: npx ts-node -r reflect-metadata src/modules/audit-logs/audit-request.selftest.ts
 */
import { buildMutationAudit, shouldAuditMutation } from './audit-request.util';

function assert(cond: unknown, msg: string): asserts cond {
  if (!cond) throw new Error(msg);
}

assert(shouldAuditMutation('GET', '/api/customers') === false, 'GET is ignored');
assert(shouldAuditMutation('POST', '/api/audit-logs') === false, 'audit-logs is ignored');
assert(shouldAuditMutation('POST', '/api/auth/refresh') === false, 'auth refresh is ignored');
assert(shouldAuditMutation('POST', '/api/customers') === true, 'customer create is audited');

const customer = buildMutationAudit({
  method: 'POST',
  url: '/api/customers',
  statusCode: 201,
  body: { id: 'c1', password: 'secret' },
  user: { sub: 'u1', email: 'a@b.c', role: 'admin' },
});
assert(customer?.entityType === 'customers', 'entity type from route');
assert(customer?.entityId === 'c1', 'entity id from body');
assert(customer?.action === 'POST /customers', 'action is method and path');
assert(customer?.source === 'central', 'central source');
assert(customer?.metadata.password === undefined, 'password is not stored');
assert(buildMutationAudit({ method: 'GET', url: '/api/products/1' }) === null, 'GET draft is null');

const bill = buildMutationAudit({
  method: 'POST',
  url: '/api/store-pos/bills',
  body: { storeId: 'S1', deviceId: 'D1', billNo: 'B-9' },
});
assert(bill?.source === 'store_billing', 'store-pos is store billing');
assert(bill?.storeId === 'S1' && bill.deviceId === 'D1', 'store and device copied');
assert(bill?.entityId === 'B-9', 'bill number is entity id');
assert(bill?.billNo === 'B-9', 'bill number is stored');

const nested = buildMutationAudit({
  method: 'POST',
  url: '/api/store-pos/bills',
  body: { storeId: 'S1', deviceId: 'D1', payload: { billNo: 'B-2', posCounter: 'C1' } },
});
assert(nested?.billNo === 'B-2', 'nested bill number');
assert(nested?.posCounter === 'C1', 'nested pos counter');

console.log('audit-request.selftest ok');
