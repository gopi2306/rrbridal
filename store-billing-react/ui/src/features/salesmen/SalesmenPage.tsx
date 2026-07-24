import type { FormEvent } from 'react';
import { useEffect, useState } from 'react';
import { api } from '../../shared/api';
import { useAuth } from '../../shared/auth';

type Salesman = {
  _id?: string;
  id?: string;
  salesmanCode?: string;
  name: string;
  phone?: string;
  isActive?: boolean;
};

export function SalesmenPage() {
  const { user } = useAuth();
  const storeId = user?.storeId || '';
  const [items, setItems] = useState<Salesman[]>([]);
  const [form, setForm] = useState({ name: '', phone: '' });
  const [message, setMessage] = useState('');

  async function load() {
    const res = await api<Salesman[] | { items: Salesman[] }>(
      `/salesmen?storeId=${encodeURIComponent(storeId)}`,
    );
    setItems(Array.isArray(res) ? res : res.items || []);
  }

  useEffect(() => {
    if (storeId) void load().catch((e: Error) => setMessage(e.message));
  }, [storeId]);

  async function onCreate(e: FormEvent) {
    e.preventDefault();
    try {
      await api('/salesmen', {
        method: 'POST',
        body: JSON.stringify({ ...form, storeId }),
      });
      setForm({ name: '', phone: '' });
      setMessage('Salesman created');
      await load();
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Failed');
    }
  }

  return (
    <div>
      <div className="page-header">
        <div>
          <h1>Salesmen</h1>
          <p>Manage store sales staff for billing attribution.</p>
        </div>
      </div>
      <div className="grid-2" style={{ alignItems: 'start' }}>
        <form className="form-card" onSubmit={(e) => void onCreate(e)}>
          <h3 style={{ marginTop: 0 }}>Register salesman</h3>
          <div className="field">
            <label>Name</label>
            <input required value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
          </div>
          <div className="field">
            <label>Phone</label>
            <input value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} />
          </div>
          <button className="btn btn-primary" type="submit">
            Save
          </button>
          {message && <p className="muted">{message}</p>}
        </form>
        <div className="panel">
          <table className="table">
            <thead>
              <tr>
                <th>Code</th>
                <th>Name</th>
                <th>Phone</th>
              </tr>
            </thead>
            <tbody>
              {items.map((s) => (
                <tr key={s._id || s.id || s.salesmanCode}>
                  <td>{s.salesmanCode || '—'}</td>
                  <td>{s.name}</td>
                  <td>{s.phone || '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
