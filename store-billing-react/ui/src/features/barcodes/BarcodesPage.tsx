import { useEffect, useState } from 'react';
import { ApiError } from '../../shared/api';
import * as storePos from '../../shared/storePos';

export function BarcodesPage() {
  const [design, setDesign] = useState<Record<string, unknown> | null>(null);
  const [sku, setSku] = useState('');
  const [copies, setCopies] = useState('1');
  const [status, setStatus] = useState('Load active barcode label design, then print SKUs.');
  const [error, setError] = useState('');

  useEffect(() => {
    void (async () => {
      setError('');
      try {
        const d = (await storePos.getBarcodeLabelDesign()) as Record<string, unknown> | null;
        setDesign(d);
        setStatus(d ? 'Active label design loaded.' : 'No active barcode label design.');
      } catch (e) {
        setError(e instanceof ApiError ? e.message : e instanceof Error ? e.message : 'Load failed');
      }
    })();
  }, []);

  function printLabels() {
    setError('');
    const codes = sku
      .split(/[\s,;]+/)
      .map((s) => s.trim())
      .filter(Boolean);
    if (!codes.length) {
      setError('Enter at least one SKU.');
      return;
    }
    const n = Math.max(1, Math.min(50, Number(copies) || 1));
    const w = window.open('', '_blank', 'noopener,noreferrer,width=480,height=640');
    if (!w) {
      setError('Pop-up blocked. Allow pop-ups to print labels.');
      return;
    }
    const labels = codes
      .flatMap((code) => Array.from({ length: n }, () => code))
      .map(
        (code) =>
          `<div class="label"><div class="sku">${code}</div><div class="meta">TruBilling barcode</div></div>`,
      )
      .join('');
    w.document.write(`<!doctype html><html><head><title>Barcode labels</title>
<style>
  body { font-family: Consolas, monospace; margin: 12px; }
  .label { border: 1px dashed #333; padding: 12px 16px; margin: 0 0 10px; width: 220px; page-break-inside: avoid; }
  .sku { font-size: 18px; font-weight: 700; letter-spacing: 0.04em; }
  .meta { font-size: 11px; color: #555; margin-top: 6px; }
  @media print { body { margin: 0; } }
</style></head><body>${labels}<script>window.onload=()=>{window.print();}</script></body></html>`);
    w.document.close();
    setStatus(`Print dialog opened for ${codes.length} SKU(s) × ${n}.`);
  }

  const summary = design
    ? {
        name: design.name ?? design.title ?? design.label ?? 'Active design',
        width: design.widthMm ?? design.width ?? design.labelWidth,
        height: design.heightMm ?? design.height ?? design.labelHeight,
        id: design._id ?? design.id,
      }
    : null;

  return (
    <div>
      <h1 className="page-title">Barcodes</h1>
      <p className="page-sub">Print product barcode labels (browser print · active central design).</p>

      <div className="card" style={{ marginBottom: 12 }}>
        <div className="section-title">Active label design</div>
        {summary ? (
          <pre style={{ margin: 0, fontSize: 12, whiteSpace: 'pre-wrap' }}>
            {JSON.stringify(summary, null, 2)}
          </pre>
        ) : (
          <p className="msg">No design loaded.</p>
        )}
      </div>

      <div className="card">
        <label className="form-label">SKU(s) — space or comma separated</label>
        <textarea
          className="field"
          rows={3}
          value={sku}
          onChange={(e) => setSku(e.target.value)}
          placeholder="SKU001 SKU002"
        />
        <label className="form-label" style={{ marginTop: 8, display: 'block' }}>Copies per SKU</label>
        <input className="field" style={{ maxWidth: 100 }} value={copies} onChange={(e) => setCopies(e.target.value)} />
        <div className="row-actions" style={{ marginTop: 10 }}>
          <button type="button" className="btn btn-primary" onClick={printLabels}>
            Print labels
          </button>
        </div>
      </div>

      <p className="msg" style={{ marginTop: 10 }}>{status}</p>
      {error ? <p className="msg error">{error}</p> : null}
    </div>
  );
}
