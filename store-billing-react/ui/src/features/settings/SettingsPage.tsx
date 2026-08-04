import { useEffect, useState } from 'react';
import { useAuth } from '../../shared/auth';
import * as storePos from '../../shared/storePos';

export function SettingsPage() {
  const { ctx, isPrimaryTill } = useAuth();
  const [store, setStore] = useState<Record<string, unknown> | null>(null);
  const [health, setHealth] = useState<{ ok: boolean; detail?: string }>({ ok: false });
  const [status, setStatus] = useState('Online-only terminal settings.');
  const [error, setError] = useState('');

  useEffect(() => {
    let alive = true;
    void (async () => {
      setError('');
      try {
        if (ctx?.storeId) {
          const s = await storePos.getStore(ctx.storeId);
          if (alive) setStore(s);
        }
      } catch (e) {
        if (alive) setError(e instanceof Error ? e.message : 'Store load failed');
      }
      try {
        const h = await storePos.syncHealth();
        if (alive) {
          setHealth({ ok: true, detail: typeof h === 'object' ? JSON.stringify(h) : String(h) });
          setStatus('Central sync health OK.');
        }
      } catch (e) {
        if (alive) {
          setHealth({ ok: false, detail: e instanceof Error ? e.message : 'Unreachable' });
          setStatus('Central unreachable.');
        }
      }
    })();
    return () => {
      alive = false;
    };
  }, [ctx?.storeId]);

  return (
    <div>
      <h1 className="page-title">Settings</h1>
      <p className="page-sub">Terminal context and store profile (online POS).</p>

      <div className="card" style={{ marginBottom: 12 }}>
        <div className="section-title">Session / terminal</div>
        <p className="msg">
          Store <strong>{ctx?.storeId}</strong> · Device <strong>{ctx?.deviceId}</strong> · Counter{' '}
          <strong>{ctx?.posCounter}</strong>
        </p>
        <p className="msg">
          Signed in as {ctx?.userName || ctx?.email || '—'}
          {ctx?.role ? ` · ${ctx.role}` : ''} · {isPrimaryTill ? 'Primary till' : 'Secondary till'}
        </p>
      </div>

      <div className="card" style={{ marginBottom: 12 }}>
        <div className="section-title">Online mode</div>
        <p className="msg">
          This React POS is <strong>online-only</strong>. There is no PreferCentralOnline toggle and no local
          Mongo / offline outbox. All billing calls go to central <code>/api/store-pos/*</code>.
        </p>
      </div>

      <div className="card" style={{ marginBottom: 12 }}>
        <div className="section-title">Sync health</div>
        <p className="msg">
          Status:{' '}
          <span className={health.ok ? '' : 'error'} style={{ fontWeight: 600 }}>
            {health.ok ? 'ONLINE' : 'UNREACHABLE'}
          </span>
        </p>
        {health.detail ? (
          <pre style={{ margin: 0, fontSize: 12, whiteSpace: 'pre-wrap' }}>{health.detail}</pre>
        ) : null}
      </div>

      <div className="card">
        <div className="section-title">Store (getStore)</div>
        {store ? (
          <pre style={{ margin: 0, fontSize: 12, whiteSpace: 'pre-wrap' }}>
            {JSON.stringify(
              {
                code: store.code ?? store.storeCode ?? ctx?.storeId,
                name: store.name ?? store.displayName,
                city: store.city,
                state: store.state,
                phone: store.phone,
                gstin: store.gstin,
              },
              null,
              2,
            )}
          </pre>
        ) : (
          <p className="msg">Store profile not loaded.</p>
        )}
      </div>

      <p className="msg" style={{ marginTop: 10 }}>{status}</p>
      {error ? <p className="msg error">{error}</p> : null}
    </div>
  );
}
