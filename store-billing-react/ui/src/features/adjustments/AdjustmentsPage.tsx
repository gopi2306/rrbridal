import type { FormEvent } from 'react';
import { useState } from 'react';
import { api } from '../../shared/api';
import { useAuth } from '../../shared/auth';

export function AdjustmentsPage() {
  const { user } = useAuth();
  const [form, setForm] = useState({ billNo: '', reason: '', notes: '' });
  const [message, setMessage] = useState('');

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    try {
      const res = await api<{ adjustmentNo: string }>('/adjustments', {
        method: 'POST',
        body: JSON.stringify({
          storeId: user?.storeId,
          billNo: form.billNo,
          reason: form.reason,
          notes: form.notes,
          lines: [],
        }),
      });
      setMessage(`Adjustment ${res.adjustmentNo} created`);
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Failed');
    }
  }

  return (
    <div>
      <div className="page-header">
        <div>
          <h1>Adjustments</h1>
          <p>Post-sale quantity/rate corrections with a reason.</p>
        </div>
      </div>
      <form className="form-card" onSubmit={(e) => void onSubmit(e)} style={{ maxWidth: 520 }}>
        <div className="field">
          <label>Bill no</label>
          <input required value={form.billNo} onChange={(e) => setForm({ ...form, billNo: e.target.value })} />
        </div>
        <div className="field">
          <label>Reason</label>
          <input required value={form.reason} onChange={(e) => setForm({ ...form, reason: e.target.value })} />
        </div>
        <div className="field">
          <label>Notes</label>
          <textarea value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} rows={3} />
        </div>
        <button className="btn btn-primary" type="submit">
          Post adjustment
        </button>
        {message && <p className="muted">{message}</p>}
      </form>
    </div>
  );
}
