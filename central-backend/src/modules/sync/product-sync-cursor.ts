import { Types } from 'mongoose';

export type ParsedProductSyncCursor =
  | { kind: 'start' }
  | { kind: 'full-catchup' }
  | { kind: 'compound'; updatedAt: Date; id: Types.ObjectId };

/**
 * Product pull cursor: `{updatedAtIso}|{objectId}`.
 * Legacy bare ObjectId triggers a one-time full product catch-up (empty filter).
 */
export function parseProductSyncCursor(sinceCursor: string | undefined | null): ParsedProductSyncCursor {
  const raw = (sinceCursor ?? '').trim();
  if (!raw || raw === '0') return { kind: 'start' };

  const pipe = raw.indexOf('|');
  if (pipe > 0) {
    const iso = raw.slice(0, pipe).trim();
    const idPart = raw.slice(pipe + 1).trim();
    const updatedAt = new Date(iso);
    if (
      !Number.isNaN(updatedAt.getTime()) &&
      Types.ObjectId.isValid(idPart) &&
      String(new Types.ObjectId(idPart)) === idPart
    ) {
      return { kind: 'compound', updatedAt, id: new Types.ObjectId(idPart) };
    }
  }

  // Legacy bare ObjectId: force full product re-pull so price edits reach stores.
  if (Types.ObjectId.isValid(raw) && String(new Types.ObjectId(raw)) === raw) {
    return { kind: 'full-catchup' };
  }

  return { kind: 'start' };
}

export function encodeProductSyncCursor(updatedAt: Date | string, id: Types.ObjectId | string): string {
  const at = updatedAt instanceof Date ? updatedAt : new Date(updatedAt);
  const iso = Number.isNaN(at.getTime()) ? new Date(0).toISOString() : at.toISOString();
  return `${iso}|${String(id)}`;
}

export function buildProductDeltaFilter(parsed: ParsedProductSyncCursor): Record<string, unknown> {
  if (parsed.kind === 'start' || parsed.kind === 'full-catchup') {
    return {};
  }

  const { updatedAt, id } = parsed;
  return {
    $or: [{ updatedAt: { $gt: updatedAt } }, { updatedAt, _id: { $gt: id } }],
  };
}

export function encodeProductSyncCursorFromProduct(
  product: { _id?: unknown; updatedAt?: unknown } | undefined | null,
  fallbackCursor: string,
): string {
  if (!product?._id) return fallbackCursor;
  const updatedAt =
    product.updatedAt instanceof Date
      ? product.updatedAt
      : typeof product.updatedAt === 'string' || typeof product.updatedAt === 'number'
        ? new Date(product.updatedAt)
        : new Date(0);
  return encodeProductSyncCursor(updatedAt, String(product._id));
}
