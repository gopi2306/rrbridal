import type { FormEvent } from 'react';
import { useEffect, useState } from 'react';
import { api } from '../../shared/api';

type Customer = {
  _id?: string;
  id?: string;
  customerCode?: string;
  name: string;
  phone?: string;
  email?: string;
  isCreditCustomer?: boolean;
};

export function CustomersPage() {
  const [items, setItems] = useState<Customer[]>([]);
  const [search, setSearch] = useState('');
  const [form, setForm] = useState({ name: '', phone: '', email: '', isCreditCustomer: false });
  const [message, setMessage] = useState('');

  async function load(q = search) {
    const qs = q.trim() ? `?search=${encodeURIComponent(q.trim())}` : '';
    const res = await api<Customer[] | { items: Customer[] }>(`/customers${qs}`);
    setItems(Array.isArray(res) ? res : res.items || []);
  }

  useEffect(() => {
    void load().catch((e: Error) => setMessage(e.message));
  }, []);

  async function onCreate(e: FormEvent) {
    e.preventDefault();
    try {
      await api('/customers', { method: 'POST', body: JSON.stringify(form) });
      setForm({ name: '', phone: '', email: '', isCreditCustomer: false });
      setMessage('Customer created');
      await load();
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Failed');
    }
  }

  return (
    <div>
      <div className="page-header">
        <div>
          <h1>Customers</h1>
          <p>Register and search store customers.</p>
        </div>
      </div>
      <div className="grid-2" style={{ alignItems: 'start' }}>
        <form className="form-card" onSubmit={(e) => void onCreate(e)}>
          <h3 style={{ marginTop: 0 }}>New customer</h3>
          <div className="field">
            <label>Name</label>
            <input required value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
          </div>
          <div className="field">
            <label>Phone</label>
            <input value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} />
          </div>
          <div className="field">
            <label>Email</label>
            <input value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
          </div>
          <label>
            <input
              type="checkbox"
              checked={form.isCreditCustomer}
              onChange={(e) => setForm({ ...form, isCreditCustomer: e.target.checked })}
            />{' '}
            Credit customer
          </label>
          <div style={{ marginTop: 12 }}>
            <button className="btn btn-primary" type="submit">
              Save
            </button>
          </div>
          {message && <p className="muted">{message}</p>}
        </form>
        <div>
          <div className="toolbar">
            <input
              style={{ flex: 1 }}
              placeholder="Search..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && void load()}
            />
            <button className="btn btn-secondary" type="button" onClick={() => void load()}>
              Search
            </button>
          </div>
          <div className="panel">
            <table className="table">
              <thead>
                <tr>
                  <th>Code</th>
                  <th>Name</th>
                  <th>Phone</th>
                  <th>Credit</th>
                </tr>
              </thead>
              <tbody>
                {items.map((c) => (
                  <tr key={c._id || c.id || c.customerCode}>
                    <td>{c.customerCode || '—'}</td>
                    <td>{c.name}</td>
                    <td>{c.phone || '—'}</td>
                    <td>{c.isCreditCustomer ? <span className="chip">Yes</span> : '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  );
}
