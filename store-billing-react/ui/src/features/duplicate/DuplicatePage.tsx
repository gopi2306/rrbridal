import { useState } from 'react';
import { ApiError } from '../../shared/api';
import { formatRupee } from '../../shared/money';
import * as storePos from '../../shared/storePos';

function payloadOf(bill: Record<string, unknown> | null) {
  if (!bill) return {};
  return ((bill.payload as Record<string, unknown>) || bill) as Record<string, unknown>;
}

export function DuplicatePage() {
  const [search, setSearch] = useState('');
  const [results, setResults] = useState<Record<string, unknown>[]>([]);
  const [bill, setBill] = useState<Record<string, unknown> | null>(null);
  const [status, setStatus] = useState('Search a bill, then print duplicate.');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  async function searchBills() {
    setError('');
    setBusy(true);
    try {
      const rows = (await storePos.listBills(search.trim() || undefined, 40)) as Record<string, unknown>[];
      setResults(Array.isArray(rows) ? rows : []);
      setStatus(rows?.length ? `${rows.length} bill(s) found.` : 'No bills found.');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Search failed');
    } finally {
      setBusy(false);
    }
  }

  async function openBill(billNo: string) {
    setError('');
    setBusy(true);
    try {
      const row = await storePos.getBill(billNo);
      setBill(row);
      setStatus(`Loaded ${billNo} — ready to print duplicate.`);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : e instanceof Error ? e.message : 'Load failed');
    } finally {
      setBusy(false);
    }
  }

  function printDuplicate() {
    if (!bill) return;
    const p = payloadOf(bill);
    const lines = (Array.isArray(p.lines) ? p.lines : []) as Record<string, unknown>[];
    const payable = Number(p.payable ?? (p.totals as Record<string, unknown> | undefined)?.payable ?? 0);
    const w = window.open('', '_blank', 'noopener,noreferrer,width=420,height=700');
    if (!w) {
      setError('Pop-up blocked. Allow pop-ups to print.');
      return;
    }
    const lineHtml = lines
      .map(
        (l) =>
          `<tr><td>${String(l.sku || '')}</td><td>${String(l.name || '')}</td><td style="text-align:right">${Number(l.qty) || 0}</td><td style="text-align:right">${Number(l.amount) || 0}</td></tr>`,
      )
      .join('');
    w.document.write(`<!doctype html><html><head><title>Duplicate ${String(bill.billNo)}</title>
<style>
  body { font-family: Consolas, monospace; font-size: 12px; margin: 16px; max-width: 360px; }
  h1 { font-size: 16px; margin: 0 0 4px; }
  .muted { color: #555; }
  table { width: 100%; border-collapse: collapse; margin-top: 12px; }
  td, th { padding: 3px 0; border-bottom: 1px dotted #ccc; }
  .total { font-weight: 700; font-size: 14px; margin-top: 12px; }
  .dup { border: 1px solid #000; display: inline-block; padding: 2px 8px; margin-bottom: 8px; }
</style></head><body>
<div class="dup">DUPLICATE</div>
<h1>Bill ${String(bill.billNo)}</h1>
<div class="muted">${String(p.customerName || 'Walk-in')} · ${String(p.customerPhone || '')}</div>
<div class="muted">${String(p.businessDate || '')}</div>
<table><thead><tr><th>SKU</th><th>Item</th><th>Qty</th><th>Amt</th></tr></thead>
<tbody>${lineHtml}</tbody></table>
<div class="total">Payable ${formatRupee(payable)}</div>
<script>window.onload=()=>{window.print();}</script>
</body></html>`);
    w.document.close();
    setStatus(`Print dialog opened for ${String(bill.billNo)}.`);
  }

  const p = payloadOf(bill);

  return (
    <div>
      <h1 className="page-title">Duplicate bill</h1>
      <p className="page-sub">Search posted bills and print a duplicate copy.</p>

      <div className="card row-actions" style={{ marginBottom: 12 }}>
        <input
          className="field"
          style={{ flex: 1 }}
          placeholder="Bill no / customer"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') void searchBills();
          }}
        />
        <button type="button" className="btn btn-outline" disabled={busy} onClick={() => void searchBills()}>
          Search
        </button>
      </div>

      <div className="card" style={{ padding: 0, overflow: 'auto', marginBottom: 12 }}>
        <table className="data">
          <thead>
            <tr>
              <th>Bill</th>
              <th>Customer</th>
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
                  <td className="num">
                    {formatRupee(Number(pl.payable ?? (pl.totals as Record<string, unknown> | undefined)?.payable ?? 0))}
                  </td>
                  <td>
                    <button type="button" className="btn btn-outline btn-sm" onClick={() => void openBill(no)}>
                      Open
                    </button>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {bill ? (
        <div className="card" id="duplicate-print">
          <div className="section-title">Duplicate — {String(bill.billNo)}</div>
          <p className="msg">
            {String(p.customerName || 'Walk-in')} · {String(p.customerPhone || '—')}
          </p>
          <p className="msg">
            Payable{' '}
            {formatRupee(Number(p.payable ?? (p.totals as Record<string, unknown> | undefined)?.payable ?? 0))}
          </p>
          <button type="button" className="btn btn-primary" onClick={printDuplicate}>
            Print duplicate
          </button>
        </div>
      ) : null}

      <p className="msg" style={{ marginTop: 10 }}>{status}</p>
      {error ? <p className="msg error">{error}</p> : null}
    </div>
  );
}
