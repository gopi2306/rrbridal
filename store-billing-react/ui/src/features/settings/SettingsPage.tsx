import { useEffect, useState } from 'react';
import { api } from '../../shared/api';
import { useAuth } from '../../shared/auth';
import type { BillingSettings } from '../../shared/types';

export function SettingsPage() {
  const { user } = useAuth();
  const storeId = user?.storeId || '';
  const [settings, setSettings] = useState<BillingSettings | null>(null);
  const [whatsapp, setWhatsapp] = useState<unknown>(null);
  const [receiptFormat, setReceiptFormat] = useState(
    () => localStorage.getItem('trubilling_receipt_format') || 'Thermal',
  );
  const [message, setMessage] = useState('');

  useEffect(() => {
    if (!storeId) return;
    void Promise.all([
      api<BillingSettings>(`/company-billing-settings?storeId=${encodeURIComponent(storeId)}`),
      api(`/whatsapp/settings?storeId=${encodeURIComponent(storeId)}`).catch(() => null),
    ]).then(([s, w]) => {
      setSettings(s);
      setWhatsapp(w);
    });
  }, [storeId]);

  async function saveBilling() {
    if (!settings) return;
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
      localStorage.setItem('trubilling_receipt_format', receiptFormat);
      setMessage('Settings saved');
    } catch (e) {
      setMessage(e instanceof Error ? e.message : 'Failed');
    }
  }

  return (
    <div>
      <div className="page-header">
        <div>
          <h1>Settings</h1>
          <p>Terminal preferences, billing mode, and WhatsApp.</p>
        </div>
      </div>

      <div className="form-card">
        <h3 style={{ marginTop: 0 }}>Session</h3>
        <p className="muted">
          Store <strong>{user?.storeId}</strong> · Device <strong>{user?.deviceId}</strong> · Counter{' '}
          <strong>{user?.posCounter}</strong> ·{' '}
          {user?.isPrimaryTill ? 'Primary till' : 'Secondary till'}
        </p>
      </div>

      <div className="form-card">
        <h3 style={{ marginTop: 0 }}>Receipt format</h3>
        <select value={receiptFormat} onChange={(e) => setReceiptFormat(e.target.value)}>
          <option>Thermal</option>
          <option>A4</option>
          <option>A5</option>
          <option>A4 Commercial</option>
        </select>
      </div>

      {settings && (
        <div className="form-card">
          <h3 style={{ marginTop: 0 }}>Billing mode</h3>
          <div className="field">
            <label>Default mode</label>
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
          <label>
            <input
              type="checkbox"
              checked={settings.allowPerBillSwitch}
              onChange={(e) =>
                setSettings({ ...settings, allowPerBillSwitch: e.target.checked })
              }
            />{' '}
            Allow per-bill switch
          </label>
        </div>
      )}

      <div className="form-card">
        <h3 style={{ marginTop: 0 }}>WhatsApp</h3>
        <pre style={{ fontSize: 12 }}>{JSON.stringify(whatsapp, null, 2)}</pre>
        <p className="muted">
          Sending invoices uses <code>POST /pos-v2/whatsapp/send-invoice</code> with a base64
          attachment (browser print/PDF export).
        </p>
      </div>

      <div className="form-card">
        <h3 style={{ marginTop: 0 }}>Payment providers</h3>
        <p className="muted">
          Pine Labs / Razorpay POS hardware SDKs are desktop-oriented. This online app records Cash /
          Card / Split / Credit modes on the server; live terminal capture is manual confirm for v1.
        </p>
      </div>

      <button className="btn btn-primary" type="button" onClick={() => void saveBilling()}>
        Save settings
      </button>
      {message && <p className="muted">{message}</p>}
    </div>
  );
}
