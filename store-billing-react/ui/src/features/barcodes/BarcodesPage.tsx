import { useEffect, useState } from 'react';
import { api } from '../../shared/api';
import { useAuth } from '../../shared/auth';
import type { CatalogItem } from '../../shared/types';

export function BarcodesPage() {
  const { user } = useAuth();
  const [design, setDesign] = useState<unknown>(null);
  const [skus, setSkus] = useState('');
  const [items, setItems] = useState<CatalogItem[]>([]);
  const [message, setMessage] = useState('');

  useEffect(() => {
    void api('/barcode-label-design')
      .then(setDesign)
      .catch((e: Error) => setMessage(e.message));
    if (user?.storeId) {
      void api<{ items: CatalogItem[] }>(`/catalog?storeId=${encodeURIComponent(user.storeId)}&limit=100`).then(
        (r) => setItems(r.items),
      );
    }
  }, [user?.storeId]);

  function printLabels() {
    const selected = skus
      .split(/[\s,]+/)
      .map((s) => s.trim())
      .filter(Boolean);
    const rows = items.filter((i) => selected.includes(i.sku) || selected.length === 0).slice(0, 20);
    const w = window.open('', '_blank');
    if (!w) return;
    w.document.write(`<html><head><title>Barcode labels</title>
      <style>
        body{font-family:monospace;padding:12px}
        .label{border:1px dashed #94a3b8;padding:10px;margin:8px;display:inline-block;width:180px}
        .sku{font-weight:700;font-size:14px}
        .name{font-size:11px;margin:4px 0}
        .bars{letter-spacing:2px;font-size:18px}
      </style></head><body>`);
    for (const r of rows) {
      w.document.write(
        `<div class="label"><div class="sku">${r.sku}</div><div class="name">${r.name}</div><div class="bars">||||| ${r.barcode || r.sku} |||||</div><div>₹${r.retailPrice}</div></div>`,
      );
    }
    w.document.write('</body></html>');
    w.document.close();
    w.print();
  }

  return (
    <div>
      <div className="page-header">
        <div>
          <h1>Barcodes</h1>
          <p>Preview and print product labels in the browser.</p>
        </div>
        <button className="btn btn-primary" type="button" onClick={printLabels}>
          Print labels
        </button>
      </div>
      {message && <div className="error">{message}</div>}
      <div className="form-card">
        <div className="field">
          <label>SKUs (comma/space separated — leave empty for first 20 catalog items)</label>
          <textarea rows={3} value={skus} onChange={(e) => setSkus(e.target.value)} />
        </div>
      </div>
      <div className="panel" style={{ padding: 14 }}>
        <strong>Active label design</strong>
        <pre style={{ fontSize: 12 }}>{JSON.stringify(design, null, 2)}</pre>
      </div>
    </div>
  );
}
