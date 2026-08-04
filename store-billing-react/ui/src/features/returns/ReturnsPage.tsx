import { useState } from 'react';
import { ApiError } from '../../shared/api';
import { useAuth } from '../../shared/auth';
import { formatRupee, round2, todayIso } from '../../shared/money';
import * as storePos from '../../shared/storePos';

type BillLine = {
  lineNo?: number;
  sku?: string;
  name?: string;
  qty?: number;
  rate?: number;
  amount?: number;
  gstPercent?: number;
};

type ReturnLine = BillLine & { selected: boolean; returnQty: number };

function payloadOf(bill: Record<string, unknown> | null) {
  if (!bill) return {};
  return ((bill.payload as Record<string, unknown>) || bill) as Record<string, unknown>;
}

export function ReturnsPage() {
  const { ctx } = useAuth();
  const [billNo, setBillNo] = useState('');
  const [bill, setBill] = useState<Record<string, unknown> | null>(null);
  const [lines, setLines] = useState<ReturnLine[]>([]);
  const [reason, setReason] = useState('');
  const [status, setStatus] = useState('Load a bill by number, select lines to return.');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  async function loadBill() {
    setError('');
    setBusy(true);
    try {
      const no = billNo.trim();
      if (!no) throw new Error('Enter original bill no.');
      const row = await storePos.getBill(no);
      setBill(row);
      const p = payloadOf(row);
      const rawLines = (Array.isArray(p.lines) ? p.lines : []) as BillLine[];
      setLines(
        rawLines.map((l, i) => ({
          ...l,
          lineNo: l.lineNo ?? i + 1,
          selected: true,
          returnQty: Number(l.qty) || 0,
        })),
      );
      setStatus(`Loaded bill ${row.billNo || no}.`);
    } catch (e) {
      setBill(null);
      setLines([]);
      setError(e instanceof ApiError ? e.message : e instanceof Error ? e.message : 'Load failed');
    } finally {
      setBusy(false);
    }
  }

  const selected = lines.filter((l) => l.selected && l.returnQty > 0);
  const returnTotal = round2(
    selected.reduce((s, l) => {
      const qty = Number(l.qty) || 0;
      const amt = Number(l.amount) || (Number(l.rate) || 0) * qty;
      const unit = qty > 0 ? amt / qty : Number(l.rate) || 0;
      return s + unit * l.returnQty;
    }, 0),
  );

  async function postReturn() {
    setError('');
    setBusy(true);
    try {
      if (!bill) throw new Error('Load a bill first.');
      if (!selected.length) throw new Error('Select at least one return line.');
      const returnNo = await storePos.nextNumber('returnNo');
      const originalBillNo = String(bill.billNo || billNo.trim());
      const p = payloadOf(bill);
      const returnLines = selected.map((l, i) => {
        const qty = Number(l.qty) || 0;
        const amt = Number(l.amount) || (Number(l.rate) || 0) * qty;
        const unit = qty > 0 ? amt / qty : Number(l.rate) || 0;
        const lineAmt = round2(unit * l.returnQty);
        return {
          lineNo: i + 1,
          sku: l.sku,
          name: l.name || l.sku,
          qty: l.returnQty,
          returnQty: l.returnQty,
          rate: Number(l.rate) || unit,
          amount: lineAmt,
          gstPercent: Number(l.gstPercent) || 0,
        };
      });
      await storePos.postSaleReturn({
        returnNo,
        originalBillNo,
        businessDate: todayIso(),
        storeId: ctx?.storeId,
        deviceId: ctx?.deviceId,
        posCounter: ctx?.posCounter,
        customerName: p.customerName,
        customerPhone: p.customerPhone,
        reason: reason.trim() || undefined,
        lines: returnLines,
        returnLines,
        totals: { payable: returnTotal },
        payable: returnTotal,
        status: 'posted',
      });
      setStatus(`Return ${returnNo} posted for ${originalBillNo}.`);
      setLines([]);
      setBill(null);
      setReason('');
    } catch (e) {
      setError(e instanceof ApiError ? e.message : e instanceof Error ? e.message : 'Post failed');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <h1 className="page-title">Sale return</h1>
      <p className="page-sub">Load bill by number, select lines to return, post return slip.</p>

      <div className="card row-actions" style={{ marginBottom: 12 }}>
        <div style={{ flex: 1, minWidth: 180 }}>
          <label className="form-label">Original bill no</label>
          <input
            className="field"
            value={billNo}
            onChange={(e) => setBillNo(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') void loadBill();
            }}
          />
        </div>
        <button type="button" className="btn btn-outline" disabled={busy} onClick={() => void loadBill()}>
          Load bill
        </button>
      </div>

      {lines.length ? (
        <div className="card" style={{ padding: 0, overflow: 'auto', marginBottom: 12 }}>
          <table className="data">
            <thead>
              <tr>
                <th></th>
                <th>SKU</th>
                <th>Name</th>
                <th className="num">Bill qty</th>
                <th className="num">Return qty</th>
                <th className="num">Rate</th>
              </tr>
            </thead>
            <tbody>
              {lines.map((l, idx) => (
                <tr key={`${l.sku}-${idx}`}>
                  <td>
                    <input
                      type="checkbox"
                      checked={l.selected}
                      onChange={(e) =>
                        setLines((prev) =>
                          prev.map((x, i) => (i === idx ? { ...x, selected: e.target.checked } : x)),
                        )
                      }
                    />
                  </td>
                  <td>{l.sku}</td>
                  <td>{l.name || '—'}</td>
                  <td className="num">{l.qty ?? 0}</td>
                  <td className="num">
                    <input
                      className="field"
                      style={{ maxWidth: 80, textAlign: 'right' }}
                      value={String(l.returnQty)}
                      onChange={(e) => {
                        const v = Math.max(0, Number(e.target.value) || 0);
                        setLines((prev) =>
                          prev.map((x, i) =>
                            i === idx ? { ...x, returnQty: Math.min(v, Number(x.qty) || v) } : x,
                          ),
                        );
                      }}
                    />
                  </td>
                  <td className="num">{formatRupee(Number(l.rate) || 0)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}

      {bill ? (
        <div className="card">
          <div className="kpi-row">
            <div className="kpi">
              <div className="label">Return total</div>
              <div className="value">{formatRupee(returnTotal)}</div>
            </div>
          </div>
          <label className="form-label">Reason</label>
          <input className="field" value={reason} onChange={(e) => setReason(e.target.value)} />
          <div className="row-actions" style={{ marginTop: 10 }}>
            <button type="button" className="btn btn-primary" disabled={busy} onClick={() => void postReturn()}>
              Post return
            </button>
          </div>
        </div>
      ) : null}

      <p className="msg" style={{ marginTop: 10 }}>{status}</p>
      {error ? <p className="msg error">{error}</p> : null}
    </div>
  );
}
