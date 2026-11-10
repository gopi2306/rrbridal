const MUTATING = new Set(['POST', 'PUT', 'PATCH', 'DELETE']);

const SECRET_KEYS = new Set([
  'password',
  'token',
  'accessToken',
  'refreshToken',
  'secret',
  'authorization',
  'pin',
]);

const ID_KEYS = ['id', '_id', 'billNo', 'invoiceNo', 'sku', 'expenseNo', 'customerId'] as const;

export type MutationAuditDraft = {
  entityType: string;
  entityId: string;
  action: string;
  source: 'central' | 'store_billing';
  storeId?: string;
  deviceId?: string;
  billNo?: string;
  posCounter?: string;
  metadata: Record<string, unknown>;
  actor?: { sub?: string; email?: string; role?: string };
};

export function shouldAuditMutation(method: string, path: string): boolean {
  if (!MUTATING.has(method.toUpperCase())) return false;
  const normalized = normalizeApiPath(path);
  const segment = firstSegment(normalized);
  if (!segment) return false;
  if (segment === 'audit-logs' || segment === 'health' || segment === 'auth') return false;
  return true;
}

export function normalizeApiPath(url: string): string {
  const path = (url.split('?')[0] ?? '/').replace(/\/+$/, '') || '/';
  return path.replace(/^\/api(?=\/|$)/, '') || '/';
}

export function buildMutationAudit(input: {
  method: string;
  url: string;
  statusCode?: number;
  params?: Record<string, unknown>;
  body?: unknown;
  user?: { sub?: string; email?: string; role?: string; storeId?: string };
}): MutationAuditDraft | null {
  const method = input.method.toUpperCase();
  const path = normalizeApiPath(input.url);
  if (!shouldAuditMutation(method, path)) return null;

  const entityType = firstSegment(path);
  const body = asRecord(input.body);
  const params = input.params ?? {};
  const entityId = firstString(params.id, params.billNo, body?.billNo, body?.invoiceNo, body?.id, body?._id) ?? '-';
  const payload = asRecord(body?.payload);
  const storeId = firstString(input.user?.storeId, body?.storeId, payload?.storeId, params.storeId);
  const deviceId = firstString(body?.deviceId, payload?.deviceId, params.deviceId);
  const billNo = firstString(
    params.billNo,
    body?.billNo,
    body?.invoiceNo,
    payload?.billNo,
    payload?.invoiceNo,
  );
  const posCounter = firstString(body?.posCounter, payload?.posCounter, params.posCounter);
  const source: MutationAuditDraft['source'] =
    entityType === 'store-pos' || entityType === 'bills' ? 'store_billing' : 'central';

  const draft: MutationAuditDraft = {
    entityType,
    entityId,
    action: `${method} ${path}`.slice(0, 240),
    source,
    metadata: {
      statusCode: input.statusCode,
      ...summarizeIds(body),
    },
  };
  if (storeId) draft.storeId = storeId;
  if (deviceId) draft.deviceId = deviceId;
  if (billNo) draft.billNo = billNo;
  if (posCounter) draft.posCounter = posCounter;
  if (input.user) {
    draft.actor = {};
    if (input.user.sub) draft.actor.sub = input.user.sub;
    if (input.user.email) draft.actor.email = input.user.email;
    if (input.user.role) draft.actor.role = input.user.role;
  }
  return draft;
}

function firstSegment(path: string): string {
  return path.split('/').filter(Boolean)[0] ?? '';
}

function asRecord(value: unknown): Record<string, unknown> | undefined {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return undefined;
  return value as Record<string, unknown>;
}

function firstString(...values: unknown[]): string | undefined {
  for (const value of values) {
    if (typeof value === 'string' && value.trim()) return value.trim();
    if (typeof value === 'number' && Number.isFinite(value)) return String(value);
  }
  return undefined;
}

function summarizeIds(body: Record<string, unknown> | undefined): Record<string, unknown> {
  if (!body) return {};
  const out: Record<string, unknown> = {};
  for (const key of ID_KEYS) {
    if (SECRET_KEYS.has(key)) continue;
    const value = body[key];
    if (typeof value === 'string' && value.trim()) out[key] = value.trim();
  }
  if (typeof body.storeId === 'string' && body.storeId.trim()) out.storeId = body.storeId.trim();
  if (typeof body.deviceId === 'string' && body.deviceId.trim()) out.deviceId = body.deviceId.trim();
  return out;
}
