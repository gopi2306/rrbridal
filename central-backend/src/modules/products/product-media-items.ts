import { normalizeMediaPublicUrl } from '../../common/media-url.util';
import { coerceObjectIdString, isValidObjectIdString } from '../../common/object-id.util';

export type ProductMediaItemValue = {
  url: string;
  description?: string;
  colourIds?: string[];
};

export type ProductMediaSource = {
  mediaItems?: unknown;
  mediaUrls?: unknown;
};

/** Resolve mediaItems from new shape or legacy mediaUrls string array. */
export function resolveProductMediaItems(doc: ProductMediaSource): ProductMediaItemValue[] {
  if (Array.isArray(doc.mediaItems) && doc.mediaItems.length > 0) {
    return normalizeMediaItemsInput(doc.mediaItems) ?? [];
  }
  if (Array.isArray(doc.mediaUrls) && doc.mediaUrls.length > 0) {
    return (normalizeMediaItemsInput(
      doc.mediaUrls.map((u) => (typeof u === 'string' ? { url: u } : u)),
    ) ?? []);
  }
  return [];
}

/**
 * Normalize an array of media items (or plain URL strings) into
 * `{ url, description? }[]`, deduping by URL (last description wins).
 */
export function normalizeMediaItemsInput(
  items: unknown,
  apiPublicOrigin?: string,
): ProductMediaItemValue[] | undefined {
  if (items === undefined) return undefined;
  if (!Array.isArray(items)) return undefined;

  const byUrl = new Map<string, ProductMediaItemValue>();
  for (const raw of items) {
    let url = '';
    let description: string | undefined;
    let colourIds: string[] | undefined;
    if (typeof raw === 'string') {
      url = raw.trim();
    } else if (raw && typeof raw === 'object') {
      const obj = raw as { url?: unknown; description?: unknown; colourIds?: unknown };
      url = typeof obj.url === 'string' ? obj.url.trim() : '';
      if (typeof obj.description === 'string') {
        const trimmed = obj.description.trim();
        description = trimmed.length > 0 ? trimmed : undefined;
      }
      if (Array.isArray(obj.colourIds)) {
        const normalized = obj.colourIds
          .map((id) => coerceObjectIdString(id))
          .filter((id): id is string => typeof id === 'string' && isValidObjectIdString(id));
        colourIds = normalized.length > 0 ? normalized : undefined;
      }
    }
    if (!url) continue;
    const normalizedUrl = normalizeMediaPublicUrl(url, apiPublicOrigin) ?? url;
    const prev = byUrl.get(normalizedUrl);
    const next: ProductMediaItemValue = { url: normalizedUrl };
    if (description !== undefined) {
      next.description = description;
    } else if (prev?.description) {
      next.description = prev.description;
    }
    if (colourIds !== undefined) {
      next.colourIds = colourIds;
    } else if (prev?.colourIds) {
      next.colourIds = prev.colourIds;
    }
    byUrl.set(normalizedUrl, next);
  }
  return Array.from(byUrl.values());
}

/** Upsert one media item by URL; updates description when the URL already exists. */
export function appendOrUpdateMediaItem(
  existing: ProductMediaItemValue[],
  url: string,
  description?: string,
  apiPublicOrigin?: string,
  colourIds?: string[],
): ProductMediaItemValue[] {
  const normalizedUrl = normalizeMediaPublicUrl(url.trim(), apiPublicOrigin) ?? url.trim();
  if (!normalizedUrl) return existing.slice();

  const desc = description?.trim() || undefined;
  const normalizedColourIds = colourIds
    ?.map((id) => id.trim())
    .filter((id) => isValidObjectIdString(id));
  const next = existing.slice();
  const idx = next.findIndex((m) => m.url === normalizedUrl);
  if (idx >= 0) {
    const updated: ProductMediaItemValue = { url: normalizedUrl };
    if (desc !== undefined) {
      updated.description = desc;
    } else if (next[idx]?.description) {
      updated.description = next[idx]?.description;
    }
    if (normalizedColourIds && normalizedColourIds.length > 0) {
      updated.colourIds = normalizedColourIds;
    } else if (next[idx]?.colourIds) {
      updated.colourIds = next[idx]?.colourIds;
    }
    next[idx] = updated;
    return next;
  }

  const created: ProductMediaItemValue = { url: normalizedUrl };
  if (desc) created.description = desc;
  if (normalizedColourIds && normalizedColourIds.length > 0) {
    created.colourIds = normalizedColourIds;
  }
  next.push(created);
  return next;
}

export function mediaItemsToLegacyUrls(items: ProductMediaItemValue[]): string[] {
  return items.map((m) => m.url).filter((u) => u.length > 0);
}
