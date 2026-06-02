/** Public path to a file under uploads (Nest global prefix `api` + media controller). */
export function mediaFilePublicPath(folder: string, filename: string): string {
  const safeFolder = folder.replace(/\\/g, '/').replace(/^\/+|\/+$/g, '');
  const safeName = filename.replace(/\\/g, '/').replace(/^\/+/, '');
  return `/api/media/files/${safeFolder}/${safeName}`;
}

/**
 * Fixes legacy upload URLs (`/media/files/...`) and optionally prefixes API origin.
 */
export function normalizeMediaPublicUrl(
  url: string | undefined,
  apiPublicOrigin?: string,
): string | undefined {
  if (!url?.trim()) return undefined;
  let u = url.trim();

  if (/^https?:\/\//i.test(u)) return u;

  if (u.startsWith('/media/files/')) {
    u = `/api${u}`;
  } else if (u.startsWith('media/files/')) {
    u = `/api/${u}`;
  } else if (!u.startsWith('/api/media/files/') && u.startsWith('/api/')) {
    /* already api-prefixed */
  } else if (!u.startsWith('/api/media/files/') && !u.startsWith('/')) {
    u = `/api/media/files/${u}`;
  }

  const origin = apiPublicOrigin?.trim().replace(/\/$/, '');
  if (origin && u.startsWith('/')) {
    return `${origin}${u}`;
  }
  return u;
}
