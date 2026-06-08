import { Types } from 'mongoose';

const SKIP_KEYS = new Set(['__v', 'createdAt', 'updatedAt']);

export type AuditActor = {
  sub?: string;
  email?: string;
  role?: string;
};

export function serializeAuditValue(value: unknown): unknown {
  if (value === undefined) return undefined;
  if (value === null) return null;
  if (value instanceof Types.ObjectId) return value.toString();
  if (value instanceof Date) return value.toISOString();
  if (Array.isArray(value)) return value.map(serializeAuditValue);
  if (typeof value === 'object') {
    const out: Record<string, unknown> = {};
    for (const [k, v] of Object.entries(value as Record<string, unknown>)) {
      out[k] = serializeAuditValue(v);
    }
    return out;
  }
  return value;
}

export function serializeAuditDocument(doc: unknown): Record<string, unknown> {
  if (!doc || typeof doc !== 'object') return {};
  return serializeAuditValue(doc) as Record<string, unknown>;
}

export function diffAuditFields(
  before: Record<string, unknown>,
  after: Record<string, unknown>,
  fields?: string[],
): { field: string; before?: unknown; after?: unknown }[] {
  const keys = fields?.length
    ? fields
    : [...new Set([...Object.keys(before), ...Object.keys(after)])];

  const changes: { field: string; before?: unknown; after?: unknown }[] = [];
  for (const field of keys) {
    if (SKIP_KEYS.has(field)) continue;
    const b = before[field];
    const a = after[field];
    if (jsonEqual(b, a)) continue;
    changes.push({ field, before: b, after: a });
  }
  return changes;
}

function jsonEqual(a: unknown, b: unknown): boolean {
  return JSON.stringify(serializeAuditValue(a)) === JSON.stringify(serializeAuditValue(b));
}

export function pickAuditFields(
  doc: Record<string, unknown>,
  fields: string[],
): Record<string, unknown> {
  const out: Record<string, unknown> = {};
  for (const field of fields) {
    if (field in doc) out[field] = doc[field];
  }
  return out;
}
