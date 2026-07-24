import type { FormEvent } from 'react';
import { useEffect, useState } from 'react';
import { api } from '../../shared/api';
import { useAuth } from '../../shared/auth';

export function ReturnsPage() {
  const { user } = useAuth();
  const storeId = user?.storeId || '';
  const [items, setItems] = useState<unknown[]>([]);
  const [form, setForm] = useState({
    billNo: '',
    returnNo: '',
    kind: 'return',
    amount: '',
    reason: '',
  });
  const [message, setMessage] = useState('');

  async function load() {
    const res = await api<{ items: unknown[] }>(`/returns?storeId=${encodeURIComponent(storeId)}`);
    setItems(res.items || []);
  }

  useEffect(() => {
    if (storeId) void load().catch((e: Error) => setMessage(e.message));
  }, [storeId]);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    try {
      await api('/returns', {
        method: 'POST',
        body: JSON.stringify({
          storeId,
          billNo: form.billNo,
          originalBillNo: form.billNo,
          returnNo: form.returnNo || undefined,
          kind: form.kind,
          amount: Number(form.amount) || 0,
          reason: form.reason,
          lines: [],
        }),
      });
      setMessage('Return posted');
      await load();
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Failed');
    }
  }

  return (
    <div>
      <div className="page-header">
        <div>
          <h1>Returns</h1>
          <p>Post sale returns and exchanges against a bill.</p>
        </div>
      </div>
      <form className="form-card" onSubmit={(e) => void onSubmit(e)}>
        <div className="grid-2">
          <div className="field">
            <label>Original bill no</label>
            <input required value={form.billNo} onChange={(e) => setForm({ ...form, billNo: e.target.value })} />
          </div>
          <div className="field">
            <label>Kind</label>
            <select value={form.kind} onChange={(e) => setForm({ ...form, kind: e.target.value })}>
              <option value="return">Return</option>
              <option value="exchange">Exchange</option>
            </select>
          </div>
          <div className="field">
            <label>Amount</label>
            <input type="number" value={form.amount} onChange={(e) => setForm({ ...form, amount: e.target.value })} />
          </div>
          <div className="field">
            <label>Reason</label>
            <input value={form.reason} onChange={(e) => setForm({ ...form, reason: e.target.value })} />
          </div>
        </div>
        <button className="btn btn-primary" type="submit">
          Post return
        </button>
        {message && <p className="muted">{message}</p>}
      </form>
      <div className="panel">
        <pre style={{ padding: 14, margin: 0, fontSize: 12 }}>{JSON.stringify(items, null, 2)}</pre>
      </div>
    </div>
  );
}
