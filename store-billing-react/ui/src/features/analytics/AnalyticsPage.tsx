import { useCallback, useEffect, useState } from 'react';
import { formatRupee } from '../../shared/money';
import * as dash from '../../shared/dashboard';

const PERIODS = [
  { value: 'today', label: 'Today' },
  { value: 'week', label: 'Last 7 days' },
  { value: 'month', label: 'This month' },
] as const;

function rowsOf(data: unknown, keys: string[]): Record<string, unknown>[] {
  if (!data || typeof data !== 'object') return [];
  const obj = data as Record<string, unknown>;
  for (const k of keys) {
    if (Array.isArray(obj[k])) return obj[k] as Record<string, unknown>[];
  }
  if (Array.isArray(data)) return data as Record<string, unknown>[];
  return [];
}

export function AnalyticsPage() {
  const [period, setPeriod] = useState('today');
  const [salesmen, setSalesmen] = useState<Record<string, unknown>[]>([]);
  const [margins, setMargins] = useState<Record<string, unknown>[]>([]);
  const [vendors, setVendors] = useState<Record<string, unknown>[]>([]);
  const [summaries, setSummaries] = useState<Record<string, Record<string, unknown>>>({});
  const [status, setStatus] = useState('Select a period and refresh analytics.');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const reload = useCallback(async () => {
    setError('');
    setBusy(true);
    try {
      const [sm, bm, vs] = await Promise.all([
        dash.getSalesmenAnalytics(period),
        dash.getBillMargin(period),
        dash.getVendorSales(period),
      ]);
      setSalesmen(rowsOf(sm, ['salesmen', 'rows', 'items']));
      setMargins(rowsOf(bm, ['bills', 'rows', 'items']));
      setVendors(rowsOf(vs, ['vendors', 'rows', 'items']));
      setSummaries({
        salesmen: ((sm as Record<string, unknown>)?.summary as Record<string, unknown>) || {},
        margin: ((bm as Record<string, unknown>)?.summary as Record<string, unknown>) || {},
        vendors: ((vs as Record<string, unknown>)?.summary as Record<string, unknown>) || {},
      });
      setStatus(`Analytics loaded for ${period}.`);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Load failed');
    } finally {
      setBusy(false);
    }
  }, [period]);

  useEffect(() => {
    void reload();
  }, [reload]);

  return (
    <div>
      <h1 className="page-title">Analytics</h1>
      <p className="page-sub">Salesman performance, bill margin, and vendor sales.</p>

      <div className="card row-actions" style={{ marginBottom: 12 }}>
        <label className="form-label" style={{ margin: 0 }}>Period</label>
        <select className="field" style={{ maxWidth: 180 }} value={period} onChange={(e) => setPeriod(e.target.value)}>
          {PERIODS.map((p) => (
            <option key={p.value} value={p.value}>
              {p.label}
            </option>
          ))}
        </select>
        <button type="button" className="btn btn-primary" disabled={busy} onClick={() => void reload()}>
          Refresh
        </button>
      </div>

      <div className="kpi-row">
        <div className="kpi">
          <div className="label">Salesmen sales</div>
          <div className="value">{formatRupee(Number(summaries.salesmen?.totalSales ?? 0))}</div>
        </div>
        <div className="kpi">
          <div className="label">Margin</div>
          <div className="value">{formatRupee(Number(summaries.margin?.totalMargin ?? 0))}</div>
        </div>
        <div className="kpi">
          <div className="label">Vendor sales</div>
          <div className="value">{formatRupee(Number(summaries.vendors?.totalSales ?? 0))}</div>
        </div>
      </div>

      <p className="msg">{status}</p>
      {error ? <p className="msg error">{error}</p> : null}

      <div className="section-title" style={{ marginTop: 16 }}>Salesmen</div>
      <div className="card" style={{ padding: 0, overflow: 'auto', marginBottom: 16 }}>
        <table className="data">
          <thead>
            <tr>
              <th>Salesman</th>
              <th className="num">Bills</th>
              <th className="num">Sales</th>
            </tr>
          </thead>
          <tbody>
            {salesmen.map((r, i) => (
              <tr key={String(r.salesmanId || r.name || i)}>
                <td>{String(r.salesmanName || r.name || '—')}</td>
                <td className="num">{String(r.billCount ?? r.bills ?? '—')}</td>
                <td className="num">{formatRupee(Number(r.totalSales ?? r.sales ?? r.payable ?? 0))}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="section-title">Bill margin</div>
      <div className="card" style={{ padding: 0, overflow: 'auto', marginBottom: 16 }}>
        <table className="data">
          <thead>
            <tr>
              <th>Bill</th>
              <th className="num">Selling</th>
              <th className="num">Cost</th>
              <th className="num">Margin</th>
            </tr>
          </thead>
          <tbody>
            {margins.slice(0, 40).map((r, i) => (
              <tr key={String(r.billNo || r.invoiceNo || i)}>
                <td>{String(r.billNo || r.invoiceNo || '—')}</td>
                <td className="num">{formatRupee(Number(r.selling ?? r.totalSelling ?? 0))}</td>
                <td className="num">{formatRupee(Number(r.cost ?? r.totalCost ?? 0))}</td>
                <td className="num">{formatRupee(Number(r.margin ?? r.marginAmount ?? 0))}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="section-title">Vendor sales</div>
      <div className="card" style={{ padding: 0, overflow: 'auto' }}>
        <table className="data">
          <thead>
            <tr>
              <th>Vendor</th>
              <th className="num">Qty</th>
              <th className="num">Sales</th>
            </tr>
          </thead>
          <tbody>
            {vendors.map((r, i) => (
              <tr key={String(r.vendorId || r.vendorName || i)}>
                <td>{String(r.vendorName || r.name || '—')}</td>
                <td className="num">{String(r.qty ?? r.totalQty ?? '—')}</td>
                <td className="num">{formatRupee(Number(r.totalSales ?? r.sales ?? 0))}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
