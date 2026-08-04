import { useCallback, useEffect, useState } from 'react';
import { ApiError } from '../../shared/api';
import { useAuth } from '../../shared/auth';
import { formatRupee, todayIso } from '../../shared/money';
import * as storePos from '../../shared/storePos';

type QuoteRow = {
  quotationNo: string;
  status: string;
  convertedBillNo?: string | null;
  payload?: Record<string, unknown>;
};

function asRows(raw: unknown): QuoteRow[] {
  return Array.isArray(raw) ? (raw as QuoteRow[]) : [];
}

export function QuotationsPage() {
  const { ctx } = useAuth();
  const [items, setItems] = useState<QuoteRow[]>([]);
  const [statusFilter, setStatusFilter] = useState('');
  const [customerName, setCustomerName] = useState('');
  const [customerPhone, setCustomerPhone] = useState('');
  const [notes, setNotes] = useState('');
  const [sku, setSku] = useState('');
  const [qty, setQty] = useState('1');
  const [rate, setRate] = useState('');
  const [status, setStatus] = useState('Filter by status or create a simple quotation.');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const reload = useCallback(async () => {
    setError('');
    try {
      const rows = asRows(await storePos.listQuotations(statusFilter || undefined, 80));
      setItems(rows);
      setStatus(rows.length ? `${rows.length} quotation(s)` : 'No quotations found.');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Load failed');
    }
  }, [statusFilter]);

  useEffect(() => {
    void reload();
  }, [reload]);

  async function createQuotation() {
    setError('');
    setBusy(true);
    try {
      if (!customerName.trim() || !customerPhone.trim()) {
        throw new Error('Enter customer name and a valid mobile number.');
      }
      const q = Number(qty) || 0;
      const r = Number(rate) || 0;
      if (!sku.trim() || q <= 0 || r <= 0) {
        throw new Error('Add SKU, qty, and rate for a simple line.');
      }
      const amount = Math.round(q * r * 100) / 100;
      const quotationNo = await storePos.nextNumber('quotationNo');
      await storePos.postQuotation({
        quotationNo,
        storeId: ctx?.storeId,
        businessDate: todayIso(),
        status: 'open',
        customerName: customerName.trim(),
        customerPhone: customerPhone.trim(),
        notes: notes.trim() || undefined,
        lines: [
          {
            lineNo: 1,
            sku: sku.trim(),
            name: sku.trim(),
            qty: q,
            rate: r,
            amount,
          },
        ],
        totals: { subTotal: amount, tax: 0, payable: amount },
        payable: amount,
      });
      setStatus(`Quotation ${quotationNo} saved.`);
      setSku('');
      setQty('1');
      setRate('');
      setNotes('');
      await reload();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : e instanceof Error ? e.message : 'Save failed');
    } finally {
      setBusy(false);
    }
  }

  async function convert(q: QuoteRow) {
    setError('');
    setBusy(true);
    try {
      const billNo = await storePos.nextNumber('billNo');
      await storePos.convertQuotation({
        quotationNo: q.quotationNo,
        convertedBillNo: billNo,
        status: 'converted',
      });
      setStatus(`Converted ${q.quotationNo} → bill ${billNo}.`);
      await reload();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Convert failed');
    } finally {
      setBusy(false);
    }
  }

  async function cancel(q: QuoteRow) {
    setError('');
    setBusy(true);
    try {
      await storePos.cancelQuotation({ quotationNo: q.quotationNo, status: 'cancelled' });
      setStatus(`Cancelled ${q.quotationNo}.`);
      await reload();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Cancel failed');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <h1 className="page-title">Quotations</h1>
      <p className="page-sub">Filter by quotation no, customer name, or mobile. Open to edit or convert to billing.</p>

      <div className="card" style={{ marginBottom: 12 }}>
        <div className="section-title">+ Create quotation</div>
        <div className="row-actions" style={{ flexWrap: 'wrap' }}>
          <div>
            <label className="form-label">Customer name</label>
            <input className="field" value={customerName} onChange={(e) => setCustomerName(e.target.value)} />
          </div>
          <div>
            <label className="form-label">Mobile</label>
            <input className="field" value={customerPhone} onChange={(e) => setCustomerPhone(e.target.value)} />
          </div>
          <div>
            <label className="form-label">SKU</label>
            <input className="field" value={sku} onChange={(e) => setSku(e.target.value)} />
          </div>
          <div>
            <label className="form-label">Qty</label>
            <input className="field" style={{ maxWidth: 80 }} value={qty} onChange={(e) => setQty(e.target.value)} />
          </div>
          <div>
            <label className="form-label">Rate</label>
            <input className="field" style={{ maxWidth: 100 }} value={rate} onChange={(e) => setRate(e.target.value)} />
          </div>
        </div>
        <label className="form-label" style={{ marginTop: 8, display: 'block' }}>Notes</label>
        <input className="field" value={notes} onChange={(e) => setNotes(e.target.value)} />
        <div className="row-actions" style={{ marginTop: 10 }}>
          <button type="button" className="btn btn-primary" disabled={busy} onClick={() => void createQuotation()}>
            Save quotation
          </button>
        </div>
      </div>

      <div className="card row-actions" style={{ marginBottom: 12 }}>
        <label className="form-label" style={{ margin: 0 }}>Status</label>
        <select className="field" style={{ maxWidth: 160 }} value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
          <option value="">All</option>
          <option value="open">Open</option>
          <option value="converted">Converted</option>
          <option value="cancelled">Cancelled</option>
        </select>
        <button type="button" className="btn btn-outline" onClick={() => void reload()}>Refresh</button>
      </div>

      <p className="msg">{status}</p>
      {error ? <p className="msg error">{error}</p> : null}

      <div className="card" style={{ padding: 0, overflow: 'auto' }}>
        <table className="data">
          <thead>
            <tr>
              <th>Quotation no</th>
              <th>Customer</th>
              <th>Mobile</th>
              <th className="num">Payable</th>
              <th>Status</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {items.map((q) => {
              const p = q.payload || {};
              const payable = Number(p.payable ?? (p.totals as Record<string, unknown> | undefined)?.payable ?? 0);
              const open = String(q.status || '').toLowerCase() === 'open';
              return (
                <tr key={q.quotationNo}>
                  <td>{q.quotationNo}</td>
                  <td>{String(p.customerName || '—')}</td>
                  <td>{String(p.customerPhone || '—')}</td>
                  <td className="num">{formatRupee(payable)}</td>
                  <td>{q.status}{q.convertedBillNo ? ` → ${q.convertedBillNo}` : ''}</td>
                  <td>
                    {open ? (
                      <div className="row-actions">
                        <button type="button" className="btn btn-primary btn-sm" disabled={busy} onClick={() => void convert(q)}>
                          Convert
                        </button>
                        <button type="button" className="btn btn-outline btn-sm" disabled={busy} onClick={() => void cancel(q)}>
                          Cancel
                        </button>
                      </div>
                    ) : null}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
