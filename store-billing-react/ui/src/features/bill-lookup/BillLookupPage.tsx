import { useCallback, useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { ApiError } from '../../shared/api';
import { formatRupee } from '../../shared/money';
import * as storePos from '../../shared/storePos';

function payloadOf(bill: Record<string, unknown> | null) {
  if (!bill) return {};
  return ((bill.payload as Record<string, unknown>) || bill) as Record<string, unknown>;
}

export function BillLookupPage() {
  const [params] = useSearchParams();
  const [search, setSearch] = useState(() => params.get('q') || '');
  const [results, setResults] = useState<Record<string, unknown>[]>([]);
  const [bill, setBill] = useState<Record<string, unknown> | null>(null);
  const [status, setStatus] = useState('Search bills by number, customer name, or phone.');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const runSearch = useCallback(async (q: string) => {
    setError('');
    setBusy(true);
    try {
      const rows = (await storePos.listBills(q.trim() || undefined, 60)) as Record<string, unknown>[];
      setResults(Array.isArray(rows) ? rows : []);
      setStatus(rows?.length ? `${rows.length} bill(s) found.` : 'No bills found.');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Search failed');
    } finally {
      setBusy(false);
    }
  }, []);

  useEffect(() => {
    const q = params.get('q');
    if (q) {
      setSearch(q);
      void runSearch(q);
    }
  }, [params, runSearch]);

  async function openBill(billNo: string) {
    setError('');
    setBusy(true);
    try {
      const row = await storePos.getBill(billNo);
      setBill(row);
      setStatus(`Detail loaded for ${billNo}.`);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : e instanceof Error ? e.message : 'Load failed');
    } finally {
      setBusy(false);
    }
  }

  const p = payloadOf(bill);
  const lines = (Array.isArray(p.lines) ? p.lines : []) as Record<string, unknown>[];

  return (
    <div>
      <h1 className="page-title">Bill lookup</h1>
      <p className="page-sub">Search listBills / open getBill detail.</p>

      <div className="card row-actions" style={{ marginBottom: 12 }}>
        <input
          className="field"
          style={{ flex: 1 }}
          placeholder="Bill no / customer / phone"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') void runSearch(search);
          }}
        />
        <button type="button" className="btn btn-primary" disabled={busy} onClick={() => void runSearch(search)}>
          Search
        </button>
      </div>

      <p className="msg">{status}</p>
      {error ? <p className="msg error">{error}</p> : null}

      <div className="card" style={{ padding: 0, overflow: 'auto', marginBottom: 12 }}>
        <table className="data">
          <thead>
            <tr>
              <th>Bill</th>
              <th>Customer</th>
              <th>Phone</th>
              <th className="num">Payable</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {results.map((r) => {
              const pl = (r.payload as Record<string, unknown>) || {};
              const no = String(r.billNo || '');
              return (
                <tr key={no}>
                  <td>{no}</td>
                  <td>{String(pl.customerName || '—')}</td>
                  <td>{String(pl.customerPhone || '—')}</td>
                  <td className="num">
                    {formatRupee(Number(pl.payable ?? (pl.totals as Record<string, unknown> | undefined)?.payable ?? 0))}
                  </td>
                  <td>
                    <button type="button" className="btn btn-outline btn-sm" onClick={() => void openBill(no)}>
                      Detail
                    </button>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {bill ? (
        <div className="card">
          <div className="section-title">Bill {String(bill.billNo)}</div>
          <div className="kpi-row">
            <div className="kpi">
              <div className="label">Customer</div>
              <div className="value" style={{ fontSize: 16 }}>{String(p.customerName || '—')}</div>
            </div>
            <div className="kpi">
              <div className="label">Payable</div>
              <div className="value">
                {formatRupee(Number(p.payable ?? (p.totals as Record<string, unknown> | undefined)?.payable ?? 0))}
              </div>
            </div>
            <div className="kpi">
              <div className="label">Balance due</div>
              <div className="value">
                {formatRupee(
                  Number(
                    p.balanceDue ??
                      (p.creditBilling as Record<string, unknown> | undefined)?.balanceDue ??
                      0,
                  ),
                )}
              </div>
            </div>
          </div>
          <table className="data" style={{ marginTop: 12 }}>
            <thead>
              <tr>
                <th>SKU</th>
                <th>Name</th>
                <th className="num">Qty</th>
                <th className="num">Amount</th>
              </tr>
            </thead>
            <tbody>
              {lines.map((l, i) => (
                <tr key={`${String(l.sku)}-${i}`}>
                  <td>{String(l.sku || '—')}</td>
                  <td>{String(l.name || '—')}</td>
                  <td className="num">{Number(l.qty) || 0}</td>
                  <td className="num">{formatRupee(Number(l.amount) || 0)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
    </div>
  );
}
