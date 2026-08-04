/**
 * Central API origin (same idea as WPF CENTRAL_API_BASE).
 * - Dev default: empty → same-origin `/api/...` via Vite proxy (avoids CORS).
 * - Set `VITE_API_BASE=http://localhost:3000` for direct calls (CORS enabled on Nest).
 */
export const API_BASE = (
  (import.meta.env.VITE_API_BASE as string | undefined) ?? ''
).replace(/\/$/, '');

const TOKEN_KEY = 'trubilling_token';
const CTX_KEY = 'trubilling_ctx';

export type TerminalContext = {
  storeId: string;
  deviceId: string;
  posCounter: string;
  email?: string;
  userName?: string;
  role?: string;
};

export function getToken() {
  return localStorage.getItem(TOKEN_KEY);
}

export function setToken(token: string | null) {
  if (token) localStorage.setItem(TOKEN_KEY, token);
  else localStorage.removeItem(TOKEN_KEY);
}

export function getContext(): TerminalContext | null {
  const raw = localStorage.getItem(CTX_KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as TerminalContext;
  } catch {
    return null;
  }
}

export function setContext(ctx: TerminalContext | null) {
  if (ctx) localStorage.setItem(CTX_KEY, JSON.stringify(ctx));
  else localStorage.removeItem(CTX_KEY);
}

export class ApiError extends Error {
  status: number;
  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}

export async function api<T = unknown>(
  path: string,
  options: RequestInit & { auth?: boolean } = {},
): Promise<T> {
  const headers = new Headers(options.headers);
  if (!headers.has('Content-Type') && options.body) {
    headers.set('Content-Type', 'application/json');
  }
  if (options.auth !== false) {
    const token = getToken();
    if (token) headers.set('Authorization', `Bearer ${token}`);
  }
  const url = path.startsWith('http')
    ? path
    : `${API_BASE}${path.startsWith('/') ? path : `/${path}`}`;
  let res: Response;
  try {
    res = await fetch(url, { ...options, headers });
  } catch (err) {
    const hint =
      err instanceof Error && err.message === 'Failed to fetch'
        ? ` Network error calling ${url}. Is central-backend running? If using absolute VITE_API_BASE, ensure CORS is enabled.`
        : '';
    throw new ApiError(0, `${err instanceof Error ? err.message : 'Request failed'}.${hint}`);
  }
  if (!res.ok) {
    let message = res.statusText;
    try {
      const body = (await res.json()) as { message?: string | string[] };
      if (Array.isArray(body.message)) message = body.message.join(', ');
      else if (body.message) message = body.message;
      else message = JSON.stringify(body).slice(0, 400);
    } catch {
      try {
        message = (await res.text()).slice(0, 400) || message;
      } catch {
        /* ignore */
      }
    }
    throw new ApiError(res.status, message);
  }
  if (res.status === 204) return undefined as T;
  const text = await res.text();
  if (!text || text === 'null') return null as T;
  return JSON.parse(text) as T;
}
