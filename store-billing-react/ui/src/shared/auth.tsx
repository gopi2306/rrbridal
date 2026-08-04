import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { api, getContext, getToken, setContext, setToken, type TerminalContext } from './api';

type AuthState = {
  ctx: TerminalContext | null;
  loading: boolean;
  isPrimaryTill: boolean;
  login: (input: {
    email: string;
    password: string;
    storeId: string;
    deviceId?: string;
    posCounter?: string;
  }) => Promise<void>;
  logout: () => void;
  refresh: () => void;
};

const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [ctx, setCtx] = useState<TerminalContext | null>(null);
  const [loading, setLoading] = useState(true);

  const refresh = useCallback(() => {
    if (!getToken()) {
      setCtx(null);
      setLoading(false);
      return;
    }
    setCtx(getContext());
    setLoading(false);
  }, []);

  useEffect(() => {
    refresh();
  }, [refresh]);

  const login = useCallback(
    async (input: {
      email: string;
      password: string;
      storeId: string;
      deviceId?: string;
      posCounter?: string;
    }) => {
      const storeId = input.storeId.trim();
      const posCounter = (input.posCounter || '1').trim() || '1';
      const deviceId = (input.deviceId || `web-${storeId}-${posCounter}`).trim();
      const res = await api<{
        accessToken: string;
        user: Record<string, unknown>;
      }>('/api/auth/login', {
        method: 'POST',
        auth: false,
        body: JSON.stringify({ email: input.email.trim(), password: input.password }),
      });
      setToken(res.accessToken);
      const next: TerminalContext = {
        storeId,
        deviceId,
        posCounter,
        email: String(res.user?.email ?? input.email),
        userName: String(res.user?.name ?? res.user?.email ?? input.email),
        role: String(res.user?.role ?? ''),
      };
      setContext(next);
      setCtx(next);
    },
    [],
  );

  const logout = useCallback(() => {
    setToken(null);
    setContext(null);
    setCtx(null);
  }, []);

  const isPrimaryTill = (ctx?.posCounter || '1') === '1';

  const value = useMemo(
    () => ({ ctx, loading, isPrimaryTill, login, logout, refresh }),
    [ctx, loading, isPrimaryTill, login, logout, refresh],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const v = useContext(AuthContext);
  if (!v) throw new Error('useAuth must be used within AuthProvider');
  return v;
}
