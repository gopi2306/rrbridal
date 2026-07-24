import { useState } from 'react';
import { api } from '../../shared/api';
import { useAuth } from '../../shared/auth';

export function DuplicatePage() {
  const { user } = useAuth();
  const [billNo, setBillNo] = useState('');
  const [detail, setDetail] = useState<unknown>(null);
  const [error, setError] = useState('');

  async function load() {
    if (!billNo.trim() || !user?.storeId) return;
    try {
      const res = await api(
        `/bills/${encodeURIComponent(billNo.trim())}?storeCode=${encodeURIComponent(user.storeId)}`,
      );
      setDetail(res);
      setError('');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Not found');
      setDetail(null);
    }
  }

  function printDuplicate() {
    const w = window.open('', '_blank');
    if (!w || !detail) return;
    w.document.write(`
      <html><head><title>DUPLICATE ${billNo}</title>
      <style>body{font-family:sans-serif;padding:24px} .wm{color:#dc2626;font-weight:800;letter-spacing:.2em}</style>
      </head><body>
      <div class="wm">DUPLICATE</div>
      <h1>Bill ${billNo}</h1>
      <pre>${JSON.stringify(detail, null, 2)}</pre>
      </body></html>`);
    w.document.close();
    w.print();
  }

  return (
    <div>
      <div className="page-header">
        <div>
          <h1>Duplicate / Reprint</h1>
          <p>Reprint a bill with a DUPLICATE watermark.</p>
        </div>
      </div>
      <div className="toolbar">
        <input
          style={{ flex: 1 }}
          placeholder="Bill number"
          value={billNo}
          onChange={(e) => setBillNo(e.target.value)}
        />
        <button className="btn btn-primary" type="button" onClick={() => void load()}>
          Load
        </button>
        <button className="btn btn-secondary" type="button" disabled={!detail} onClick={printDuplicate}>
          Print duplicate
        </button>
      </div>
      {error && <div className="error">{error}</div>}
      <div className="panel" style={{ padding: 14 }}>
        <pre style={{ margin: 0, fontSize: 12 }}>{detail ? JSON.stringify(detail, null, 2) : 'Load a bill'}</pre>
      </div>
    </div>
  );
}
