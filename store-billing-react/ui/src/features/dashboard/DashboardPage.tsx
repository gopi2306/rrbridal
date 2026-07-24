import { useEffect, useState } from 'react';
import { api } from '../../shared/api';
import { useAuth } from '../../shared/auth';
import { formatMoney } from '../../shared/money';

type Dash = {
  invoiceCount: number;
  salesTotal: number;
  recentBills: Array<{ billNo: string; payable: number; createdAt?: string }>;
  trend: Array<{ date: string; total: number }>;
};

export function DashboardPage() {
  const { user } = useAuth();
  const [data, setData] = useState<Dash | null>(null);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!user?.storeId) return;
    void api<Dash>(`/dashboard?storeId=${encodeURIComponent(user.storeId)}`)
      .then(setData)
      .catch((e: Error) => setError(e.message));
  }, [user?.storeId]);

  return (
    <div>
      <div className="page-header">
        <div>
          <h1>Dashboard</h1>
          <p>Store overview for the last 14 days.</p>
        </div>
      </div>
      {error && <div className="error">{error}</div>}
      {data && (
        <>
          <div className="kpi-grid">
            <div className="kpi-card">
              <label>Invoices</label>
              <strong>{data.invoiceCount}</strong>
            </div>
            <div className="kpi-card">
              <label>Sales total</label>
              <strong>{formatMoney(data.salesTotal)}</strong>
            </div>
          </div>
          <div className="grid-2">
            <div className="panel" style={{ padding: 14 }}>
              <strong>14-day trend</strong>
              <ul>
                {data.trend.map((t) => (
                  <li key={t.date}>
                    {t.date}: {formatMoney(t.total)}
                  </li>
                ))}
              </ul>
            </div>
            <div className="panel">
              <table className="table">
                <thead>
                  <tr>
                    <th>Recent bill</th>
                    <th>Payable</th>
                  </tr>
                </thead>
                <tbody>
                  {data.recentBills.map((b) => (
                    <tr key={b.billNo}>
                      <td>{b.billNo}</td>
                      <td>{formatMoney(b.payable)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </>
      )}
    </div>
  );
}

export function AnalyticsPage() {
  const { user } = useAuth();
  const [data, setData] = useState<Dash | null>(null);

  useEffect(() => {
    if (!user?.storeId) return;
    void api<Dash>(`/analytics?storeId=${encodeURIComponent(user.storeId)}`).then(setData);
  }, [user?.storeId]);

  const max = Math.max(1, ...(data?.trend.map((t) => t.total) || [1]));

  return (
    <div>
      <div className="page-header">
        <div>
          <h1>Analytics</h1>
          <p>14-day sales trend.</p>
        </div>
      </div>
      <div className="panel" style={{ padding: 20 }}>
        <div style={{ display: 'flex', alignItems: 'flex-end', gap: 8, height: 180 }}>
          {(data?.trend || []).map((t) => (
            <div key={t.date} style={{ flex: 1, textAlign: 'center' }}>
              <div
                style={{
                  height: `${(t.total / max) * 140}px`,
                  background: 'var(--blue)',
                  borderRadius: '6px 6px 0 0',
                  minHeight: 4,
                }}
                title={formatMoney(t.total)}
              />
              <div className="muted" style={{ fontSize: 10, marginTop: 6 }}>
                {t.date.slice(5)}
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
