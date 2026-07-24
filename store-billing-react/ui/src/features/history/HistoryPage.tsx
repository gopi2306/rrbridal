import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { api } from '../../shared/api';
import { useAuth } from '../../shared/auth';
import { formatMoney } from '../../shared/money';

type BillRow = {
  billNo: string;
  customerName?: string | null;
  netAmount?: number;
  payable?: number;
  paymentMode?: string;
  status?: string;
};

export function HistoryPage() {
  const { user } = useAuth();
  const storeId = user?.storeId || '';
  const [params, setParams] = useSearchParams();
  const [search, setSearch] = useState(params.get('q') || '');
  const [rows, setRows] = useState<BillRow[]>([]);
  const [selected, setSelected] = useState<unknown>(null);
  const [error, setError] = useState('');

  async function load(q = search) {
    if (!storeId) return;
    const qs = new URLSearchParams({ storeCode: storeId, page: '1', limit: '50' });
    if (q.trim()) qs.set('search', q.trim());
    try {
      const res = await api<{ data?: BillRow[]; items?: BillRow[] }>(`/bills?${qs}`);
      setRows(res.data || res.items || []);
      setError('');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed');
    }
  }

  useEffect(() => {
    void load(params.get('q') || '');
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [storeId]);

  async function openBill(billNo: string) {
    try {
      const detail = await api(`/bills/${encodeURIComponent(billNo)}?storeCode=${encodeURIComponent(storeId)}`);
      setSelected(detail);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to open bill');
    }
  }

  function printSelected() {
    const w = window.open('', '_blank');
    if (!w) return;
    w.document.write(`<pre>${JSON.stringify(selected, null, 2)}</pre>`);
    w.document.close();
    w.print();
  }

  return (
    <div>
      <div className="page-header">
        <div>
          <div className="crumb">Billing &gt; History</div>
          <h1>Bill Lookup</h1>
          <p>Search posted invoices for this store.</p>
        </div>
      </div>
      <div className="toolbar">
        <input
          style={{ flex: 1 }}
          placeholder="Search bill no, customer, phone..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              setParams(search ? { q: search } : {});
              void load(search);
            }
          }}
        />
        <button
          className="btn btn-primary"
          type="button"
          onClick={() => {
            setParams(search ? { q: search } : {});
            void load(search);
          }}
        >
          Search
        </button>
      </div>
      {error && <div className="error">{error}</div>}
      <div className="grid-2" style={{ alignItems: 'start' }}>
        <div className="panel">
          <table className="table">
            <thead>
              <tr>
                <th>Bill</th>
                <th>Customer</th>
                <th>Payable</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.billNo}>
                  <td className="linkish">{r.billNo}</td>
                  <td>{r.customerName || '—'}</td>
                  <td>{formatMoney(Number(r.netAmount ?? r.payable ?? 0))}</td>
                  <td>
                    <button
                      className="btn btn-secondary"
                      type="button"
                      onClick={() => void openBill(r.billNo)}
                    >
                      View
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {!rows.length && <div className="empty">No bills found</div>}
        </div>
        <div className="panel" style={{ padding: 14 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between' }}>
            <strong>Detail</strong>
            {selected != null ? (
              <button className="btn btn-secondary" type="button" onClick={printSelected}>
                Print
              </button>
            ) : null}
          </div>
          <pre style={{ fontSize: 12, overflow: 'auto', maxHeight: 480 }}>
            {selected ? JSON.stringify(selected, null, 2) : 'Select a bill'}
          </pre>
        </div>
      </div>
    </div>
  );
}
