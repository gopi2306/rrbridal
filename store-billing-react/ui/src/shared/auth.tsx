import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { api, getToken, setToken } from './api';
import type { SessionUser } from './types';

type AuthState = {
  user: SessionUser | null;
  store: Record<string, unknown> | null;
  loading: boolean;
  login: (input: {
    email: string;
    password: string;
    storeId: string;
    deviceId?: string;
    posCounter?: string;
  }) => Promise<void>;
  logout: () => Promise<void>;
  refresh: () => Promise<void>;
};

const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<SessionUser | null>(null);
  const [store, setStore] = useState<Record<string, unknown> | null>(null);
  const [loading, setLoading] = useState(true);

  const refresh = useCallback(async () => {
    if (!getToken()) {
      setUser(null);
      setStore(null);
      setLoading(false);
      return;
    }
    try {
      const session = await api<{ user: SessionUser; store: Record<string, unknown> | null }>(
        '/session',
      );
      setUser(session.user);
      setStore(session.store);
    } catch {
      setToken(null);
      setUser(null);
      setStore(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const login = useCallback(
    async (input: {
      email: string;
      password: string;
      storeId: string;
      deviceId?: string;
      posCounter?: string;
    }) => {
      const res = await api<{
        accessToken: string;
        session: { user: SessionUser; store: Record<string, unknown> | null };
      }>('/auth/login', {
        method: 'POST',
        auth: false,
        body: JSON.stringify(input),
      });
      setToken(res.accessToken);
      setUser(res.session.user);
      setStore(res.session.store);
    },
    [],
  );

  const logout = useCallback(async () => {
    try {
      await api('/auth/logout', { method: 'POST' });
    } catch {
      /* ignore */
    }
    setToken(null);
    setUser(null);
    setStore(null);
  }, []);

  const value = useMemo(
    () => ({ user, store, loading, login, logout, refresh }),
    [user, store, loading, login, logout, refresh],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
