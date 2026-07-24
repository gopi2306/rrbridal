import type { FormEvent } from 'react';
import { useEffect, useState } from 'react';
import { api } from '../../shared/api';
import { useAuth } from '../../shared/auth';

type Quote = {
  quotationNo: string;
  status: string;
  convertedBillNo?: string;
  payload: Record<string, unknown>;
};

export function QuotationsPage() {
  const { user } = useAuth();
  const storeId = user?.storeId || '';
  const [items, setItems] = useState<Quote[]>([]);
  const [customerName, setCustomerName] = useState('');
  const [notes, setNotes] = useState('');
  const [message, setMessage] = useState('');

  async function load() {
    const res = await api<{ items: Quote[] }>(`/quotations?storeId=${encodeURIComponent(storeId)}`);
    setItems(res.items || []);
  }

  useEffect(() => {
    if (storeId) void load().catch((e: Error) => setMessage(e.message));
  }, [storeId]);

  async function save(e: FormEvent) {
    e.preventDefault();
    try {
      const res = await api<{ quotationNo: string }>('/quotations', {
        method: 'POST',
        body: JSON.stringify({
          storeId,
          customerName,
          notes,
          lines: [],
          status: 'open',
          payable: 0,
        }),
      });
      setMessage(`Saved ${res.quotationNo}`);
      setCustomerName('');
      setNotes('');
      await load();
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Failed');
    }
  }

  async function convert(q: Quote) {
    try {
      await api('/quotations/convert', {
        method: 'POST',
        body: JSON.stringify({
          storeId,
          quotationNo: q.quotationNo,
          convertedBillNo: `TB-Q-${Date.now()}`,
        }),
      });
      setMessage(`Converted ${q.quotationNo}`);
      await load();
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Failed');
    }
  }

  async function cancel(q: Quote) {
    try {
      await api('/quotations/cancel', {
        method: 'POST',
        body: JSON.stringify({ storeId, quotationNo: q.quotationNo }),
      });
      await load();
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Failed');
    }
  }

  return (
    <div>
      <div className="page-header">
        <div>
          <h1>Quotations</h1>
          <p>Create quotes and convert them to billing.</p>
        </div>
      </div>
      <form className="form-card" onSubmit={(e) => void save(e)} style={{ maxWidth: 480 }}>
        <div className="field">
          <label>Customer</label>
          <input required value={customerName} onChange={(e) => setCustomerName(e.target.value)} />
        </div>
        <div className="field">
          <label>Notes</label>
          <textarea value={notes} onChange={(e) => setNotes(e.target.value)} rows={2} />
        </div>
        <button className="btn btn-primary" type="submit">
          Save quotation
        </button>
        {message && <p className="muted">{message}</p>}
      </form>
      <div className="panel">
        <table className="table">
          <thead>
            <tr>
              <th>No</th>
              <th>Customer</th>
              <th>Status</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {items.map((q) => (
              <tr key={q.quotationNo}>
                <td>{q.quotationNo}</td>
                <td>{String(q.payload.customerName || '—')}</td>
                <td>
                  <span className="chip">{q.status}</span>
                </td>
                <td style={{ display: 'flex', gap: 6 }}>
                  {q.status === 'open' && (
                    <>
                      <button className="btn btn-primary" type="button" onClick={() => void convert(q)}>
                        Convert
                      </button>
                      <button className="btn btn-secondary" type="button" onClick={() => void cancel(q)}>
                        Cancel
                      </button>
                    </>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
