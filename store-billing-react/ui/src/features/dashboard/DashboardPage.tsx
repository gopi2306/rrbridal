import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../shared/auth';
import { formatRupee, todayIso } from '../../shared/money';
import * as dash from '../../shared/dashboard';

function num(v: unknown) {
  return Number(v ?? 0);
}

export function DashboardPage() {
  const { ctx } = useAuth();
  const navigate = useNavigate();
  const [ops, setOps] = useState<Record<string, unknown> | null>(null);
  const [sales, setSales] = useState<Record<string, unknown> | null>(null);
  const [dayClose, setDayClose] = useState<Record<string, unknown> | null>(null);
  const [status, setStatus] = useState('Loading store metrics…');
  const [error, setError] = useState('');

  const reload = useCallback(async () => {
    setError('');
    try {
      const [o, s, d] = await Promise.all([
        dash.getStoreOps(),
        dash.getStoreSales('today'),
        dash.getStoreDayClose(todayIso(), ctx?.posCounter),
      ]);
      setOps(o);
      setSales(s);
      setDayClose(d);
      setStatus('Metrics refreshed.');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Load failed');
    }
  }, [ctx?.posCounter]);

  useEffect(() => {
    void reload();
  }, [reload]);

  const salesSummary = (sales?.summary as Record<string, unknown>) || {};
  const daySummary = (dayClose?.summary as Record<string, unknown>) || dayClose || {};
  const opsSummary = (ops?.summary as Record<string, unknown>) || ops || {};

  return (
    <div>
      <h1 className="page-title">Dashboard</h1>
      <p className="page-sub">
        {ctx?.storeId} · Till {ctx?.posCounter} · business date {todayIso()}
      </p>

      <div className="row-actions" style={{ marginBottom: 12 }}>
        <button type="button" className="btn btn-primary" onClick={() => void reload()}>
          Refresh metrics
        </button>
        <button type="button" className="btn btn-outline" onClick={() => navigate('/online-sales')}>
          Online sales
        </button>
        <button type="button" className="btn btn-outline" onClick={() => navigate('/credit-bills')}>
          Credit bills
        </button>
      </div>

      <div className="section-title">Today & week (posted bills)</div>
      <div className="kpi-row">
        <div className="kpi">
          <div className="label">Today sales</div>
          <div className="value">
            {formatRupee(num(salesSummary.totalSales ?? salesSummary.todaySales ?? salesSummary.payable))}
          </div>
        </div>
        <div className="kpi">
          <div className="label">Today bills</div>
          <div className="value">{String(salesSummary.billCount ?? salesSummary.todayBillCount ?? '—')}</div>
        </div>
        <div className="kpi">
          <div className="label">Day-close bills</div>
          <div className="value">{String(daySummary.billCount ?? '—')}</div>
        </div>
        <div className="kpi">
          <div className="label">Expected cash</div>
          <div className="value">{formatRupee(num(daySummary.expectedCash))}</div>
        </div>
      </div>

      <div className="section-title">Store ops</div>
      <div className="kpi-row">
        <div className="kpi">
          <div className="label">Products / stock</div>
          <div className="value">
            {String(opsSummary.productCount ?? opsSummary.catalogCount ?? ops?.productCount ?? '—')}
          </div>
        </div>
        <div className="kpi">
          <div className="label">Available qty</div>
          <div className="value">
            {String(opsSummary.totalAvailableQty ?? opsSummary.availableQty ?? '—')}
          </div>
        </div>
        <div className="kpi">
          <div className="label">COD pending</div>
          <div className="value">
            {formatRupee(num(opsSummary.codPending ?? opsSummary.onlineCodPending ?? daySummary.onlineCodPending))}
          </div>
        </div>
        <div className="kpi">
          <div className="label">Credit pending</div>
          <div className="value">
            {formatRupee(num(opsSummary.creditPending ?? opsSummary.creditBalanceDue ?? daySummary.creditPending))}
          </div>
        </div>
      </div>

      <p className="msg">{status}</p>
      {error ? <p className="msg error">{error}</p> : null}

      <div className="card" style={{ marginTop: 12 }}>
        <div className="section-title">Day close snapshot</div>
        <pre style={{ margin: 0, fontSize: 12, whiteSpace: 'pre-wrap' }}>
          {JSON.stringify(
            {
              billCount: daySummary.billCount,
              totalQty: daySummary.totalQty,
              expectedCash: daySummary.expectedCash,
              cashSales: daySummary.cashSales,
              cardSales: daySummary.cardSales,
              upiSales: daySummary.upiSales,
            },
            null,
            2,
          )}
        </pre>
      </div>
    </div>
  );
}
