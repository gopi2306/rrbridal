import { useEffect, useState } from 'react';
import { api } from '../../shared/api';
import { useAuth } from '../../shared/auth';
import type { BillingSettings } from '../../shared/types';

export function SyncCenterPage() {
  const { user } = useAuth();
  const storeId = user?.storeId || '';
  const [health, setHealth] = useState<{ ok: boolean; version?: string } | null>(null);
  const [settings, setSettings] = useState<BillingSettings | null>(null);
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);

  async function refresh() {
    if (!storeId) return;
    const [h, s] = await Promise.all([
      api<{ ok: boolean; version?: string }>('/health', { auth: false }),
      api<BillingSettings>(`/company-billing-settings?storeId=${encodeURIComponent(storeId)}`),
    ]);
    setHealth(h);
    setSettings(s);
  }

  useEffect(() => {
    void refresh().catch((e: Error) => setMessage(e.message));
  }, [storeId]);

  async function save() {
    if (!settings || !storeId) return;
    setBusy(true);
    try {
      const next = await api<BillingSettings>(
        `/company-billing-settings?storeId=${encodeURIComponent(storeId)}`,
        {
          method: 'PATCH',
          body: JSON.stringify({
            mode: settings.mode,
            allowPerBillSwitch: settings.allowPerBillSwitch,
          }),
        },
      );
      setSettings(next);
      setMessage('Billing mode saved');
    } catch (e) {
      setMessage(e instanceof Error ? e.message : 'Save failed');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <div className="page-header">
        <div>
          <div className="crumb">System &gt; Sync Center</div>
          <h1>Sync Center</h1>
          <p>Online connection status and company billing mode for this store.</p>
        </div>
        <button className="btn btn-primary" type="button" onClick={() => void refresh()}>
          Refresh status
        </button>
      </div>

      <div className="kpi-grid">
        <div className="kpi-card">
          <label>API Health</label>
          <strong style={{ color: health?.ok ? 'var(--ok)' : 'var(--danger)' }}>
            {health?.ok ? 'Operational' : 'Down'}
          </strong>
        </div>
        <div className="kpi-card">
          <label>Surface</label>
          <strong>pos-v2</strong>
        </div>
        <div className="kpi-card">
          <label>Version</label>
          <strong>{health?.version || '—'}</strong>
        </div>
        <div className="kpi-card">
          <label>Mode</label>
          <strong>{settings?.mode || '—'}</strong>
        </div>
      </div>

      <div className="form-card">
        <h3 style={{ marginTop: 0 }}>Company billing settings</h3>
        {settings && (
          <>
            <div className="field">
              <label>Default price mode</label>
              <select
                value={settings.mode}
                onChange={(e) =>
                  setSettings({ ...settings, mode: e.target.value as 'retail' | 'wholesale' })
                }
              >
                <option value="retail">Retail</option>
                <option value="wholesale">Wholesale</option>
              </select>
            </div>
            <label style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
              <input
                type="checkbox"
                checked={settings.allowPerBillSwitch}
                onChange={(e) =>
                  setSettings({ ...settings, allowPerBillSwitch: e.target.checked })
                }
              />
              Allow cashiers to switch retail/wholesale per bill
            </label>
            <div style={{ marginTop: 14 }}>
              <button className="btn btn-primary" type="button" disabled={busy} onClick={() => void save()}>
                Save settings
              </button>
            </div>
          </>
        )}
        {message && <p className="muted">{message}</p>}
      </div>
    </div>
  );
}
