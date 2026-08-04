import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { ApiError } from '../../shared/api';
import type { CustomerRow } from '../../shared/types';
import * as storePos from '../../shared/storePos';

export function CustomersPage() {
  const [items, setItems] = useState<CustomerRow[]>([]);
  const [q, setQ] = useState('');
  const [name, setName] = useState('');
  const [phone, setPhone] = useState('');
  const [isCreditCustomer, setIsCreditCustomer] = useState(false);
  const [status, setStatus] = useState('Search or register customers.');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const reload = useCallback(async (search = '') => {
    setError('');
    try {
      const rows = (await storePos.listCustomers(search)) as CustomerRow[];
      setItems(Array.isArray(rows) ? rows : []);
      setStatus(rows?.length ? `${rows.length} customer(s)` : 'No customers found.');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Load failed');
    }
  }, []);

  useEffect(() => {
    void reload('');
  }, [reload]);

  async function create(e: FormEvent) {
    e.preventDefault();
    setError('');
    setBusy(true);
    try {
      if (!name.trim()) throw new Error('Enter customer name.');
      await storePos.createCustomer({
        name: name.trim(),
        phone: phone.trim() || undefined,
        isCreditCustomer,
        isActive: true,
      });
      setStatus(`Customer ${name.trim()} created.`);
      setName('');
      setPhone('');
      setIsCreditCustomer(false);
      await reload(q);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : err instanceof Error ? err.message : 'Create failed');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <h1 className="page-title">Customers</h1>
      <p className="page-sub">Find customers by name or mobile · register new party.</p>

      <div className="card row-actions" style={{ marginBottom: 12 }}>
        <input
          className="field"
          style={{ flex: 1 }}
          placeholder="Search name / phone"
          value={q}
          onChange={(e) => setQ(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') void reload(q);
          }}
        />
        <button type="button" className="btn btn-outline" onClick={() => void reload(q)}>
          Search
        </button>
      </div>

      <form className="card" onSubmit={(e) => void create(e)} style={{ marginBottom: 12 }}>
        <div className="section-title">Register customer</div>
        <label className="form-label">Name</label>
        <input className="field" value={name} onChange={(e) => setName(e.target.value)} required />
        <label className="form-label" style={{ marginTop: 8, display: 'block' }}>Mobile</label>
        <input className="field" value={phone} onChange={(e) => setPhone(e.target.value)} />
        <label className="checks" style={{ marginTop: 10 }}>
          <input
            type="checkbox"
            checked={isCreditCustomer}
            onChange={(e) => setIsCreditCustomer(e.target.checked)}
          />{' '}
          Credit customer
        </label>
        <div className="row-actions" style={{ marginTop: 10 }}>
          <button type="submit" className="btn btn-primary" disabled={busy}>
            Create customer
          </button>
        </div>
      </form>

      <p className="msg">{status}</p>
      {error ? <p className="msg error">{error}</p> : null}

      <div className="card" style={{ padding: 0, overflow: 'auto', marginTop: 12 }}>
        <table className="data">
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
              <tr key={String(c._id || c.id || c.code || c.phone || c.name)}>
                <td>{c.code || '—'}</td>
                <td>{c.name || '—'}</td>
                <td>{c.phone || '—'}</td>
                <td>{c.isCreditCustomer ? 'Yes' : '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
