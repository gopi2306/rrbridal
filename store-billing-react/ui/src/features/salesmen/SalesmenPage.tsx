import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { ApiError } from '../../shared/api';
import { useAuth } from '../../shared/auth';
import type { SalesmanRow } from '../../shared/types';
import * as storePos from '../../shared/storePos';

export function SalesmenPage() {
  const { ctx } = useAuth();
  const [items, setItems] = useState<SalesmanRow[]>([]);
  const [name, setName] = useState('');
  const [phone, setPhone] = useState('');
  const [code, setCode] = useState('');
  const [status, setStatus] = useState('Store salesmen for this till.');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const reload = useCallback(async () => {
    setError('');
    try {
      const rows = (await storePos.listSalesmen()) as SalesmanRow[];
      setItems(Array.isArray(rows) ? rows : []);
      setStatus(rows?.length ? `${rows.length} salesman(men)` : 'No salesmen yet.');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Load failed');
    }
  }, []);

  useEffect(() => {
    void reload();
  }, [reload]);

  async function create(e: FormEvent) {
    e.preventDefault();
    setError('');
    setBusy(true);
    try {
      if (!name.trim()) throw new Error('Enter salesman name.');
      if (!ctx?.storeId) throw new Error('Store context missing.');
      await storePos.createSalesman({
        storeId: ctx.storeId,
        name: name.trim(),
        phone: phone.trim() || undefined,
        salesmanCode: code.trim() || undefined,
        isActive: true,
      });
      setStatus(`Salesman ${name.trim()} created.`);
      setName('');
      setPhone('');
      setCode('');
      await reload();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : err instanceof Error ? err.message : 'Create failed');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <h1 className="page-title">Salesman</h1>
      <p className="page-sub">Maintain store salesmen for billing attribution.</p>

      <form className="card" onSubmit={(e) => void create(e)} style={{ marginBottom: 12 }}>
        <div className="section-title">Add salesman</div>
        <label className="form-label">Name</label>
        <input className="field" value={name} onChange={(e) => setName(e.target.value)} required />
        <label className="form-label" style={{ marginTop: 8, display: 'block' }}>Phone</label>
        <input className="field" value={phone} onChange={(e) => setPhone(e.target.value)} />
        <label className="form-label" style={{ marginTop: 8, display: 'block' }}>Code (optional)</label>
        <input className="field" value={code} onChange={(e) => setCode(e.target.value)} placeholder="Auto if blank" />
        <div className="row-actions" style={{ marginTop: 10 }}>
          <button type="submit" className="btn btn-primary" disabled={busy}>
            Create salesman
          </button>
          <button type="button" className="btn btn-outline" onClick={() => void reload()}>
            Refresh
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
              <th>Active</th>
            </tr>
          </thead>
          <tbody>
            {items.map((s) => (
              <tr key={String(s._id || s.id || s.code || s.name)}>
                <td>{s.code || (s as { salesmanCode?: string }).salesmanCode || '—'}</td>
                <td>{s.name || s.displayLabel || '—'}</td>
                <td>{s.phone || '—'}</td>
                <td>{s.isActive === false ? 'No' : 'Yes'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
