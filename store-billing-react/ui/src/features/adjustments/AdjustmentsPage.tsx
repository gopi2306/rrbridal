import { useCallback, useEffect, useState } from 'react';
import { ApiError } from '../../shared/api';
import { useAuth } from '../../shared/auth';
import { formatRupee, round2, todayIso } from '../../shared/money';
import * as storePos from '../../shared/storePos';

type AdjRow = {
  adjustmentNo?: string;
  payload?: Record<string, unknown>;
  createdAt?: string;
};

export function AdjustmentsPage() {
  const { ctx } = useAuth();
  const [originalBillNo, setOriginalBillNo] = useState('');
  const [amount, setAmount] = useState('');
  const [reason, setReason] = useState('');
  const [items, setItems] = useState<AdjRow[]>([]);
  const [status, setStatus] = useState('Enter original bill no, amount, and reason.');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const reload = useCallback(async (bill?: string) => {
    setError('');
    try {
      const rows = (await storePos.listAdjustments(bill?.trim() || undefined, 100)) as AdjRow[];
      setItems(Array.isArray(rows) ? rows : []);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'List failed');
    }
  }, []);

  useEffect(() => {
    void reload();
  }, [reload]);

  async function postAdjustment() {
    setError('');
    setBusy(true);
    try {
      const bill = originalBillNo.trim();
      if (!bill) throw new Error('Enter original bill no.');
      const diff = round2(Number(amount) || 0);
      if (!diff) throw new Error('Enter a non-zero adjustment amount.');
      if (!reason.trim()) throw new Error('Enter a reason.');
      const adjustmentNo = await storePos.nextNumber('adjustmentNo');
      let originalPayable = 0;
      try {
        const billRow = await storePos.getBill(bill);
        const p = (billRow.payload as Record<string, unknown>) || billRow;
        originalPayable = Number(
          p.payable ?? (p.totals as Record<string, unknown> | undefined)?.payable ?? 0,
        );
      } catch {
        /* bill may be missing locally on central — still allow value adjust */
      }
      const adjustedPayable = round2(originalPayable + diff);
      await storePos.postAdjustmentBill({
        adjustmentNo,
        originalBillNo: bill,
        storeId: ctx?.storeId,
        deviceId: ctx?.deviceId,
        posCounter: ctx?.posCounter,
        businessDate: todayIso(),
        reason: reason.trim(),
        originalPayable,
        adjustedPayable,
        diffPayable: diff,
        amount: diff,
        lines: [],
        status: 'posted',
      });
      setStatus(`Adjustment ${adjustmentNo} posted.`);
      setAmount('');
      setReason('');
      await reload(bill);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : e instanceof Error ? e.message : 'Post failed');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <h1 className="page-title">Adjustment bill</h1>
      <p className="page-sub">Bill value / stock journal-style adjust against an original invoice.</p>

      <div className="card" style={{ marginBottom: 12 }}>
        <label className="form-label">Original bill no</label>
        <input className="field" value={originalBillNo} onChange={(e) => setOriginalBillNo(e.target.value)} />
        <label className="form-label" style={{ marginTop: 8, display: 'block' }}>
          Amount (diff payable, +/-)
        </label>
        <input className="field" value={amount} onChange={(e) => setAmount(e.target.value)} />
        <label className="form-label" style={{ marginTop: 8, display: 'block' }}>Reason</label>
        <input className="field" value={reason} onChange={(e) => setReason(e.target.value)} />
        <div className="row-actions" style={{ marginTop: 10 }}>
          <button type="button" className="btn btn-primary" disabled={busy} onClick={() => void postAdjustment()}>
            Post adjustment
          </button>
          <button
            type="button"
            className="btn btn-outline"
            onClick={() => void reload(originalBillNo)}
          >
            Refresh list
          </button>
        </div>
      </div>

      <p className="msg">{status}</p>
      {error ? <p className="msg error">{error}</p> : null}

      <div className="card" style={{ padding: 0, overflow: 'auto', marginTop: 12 }}>
        <table className="data">
          <thead>
            <tr>
              <th>Adjustment no</th>
              <th>Original bill</th>
              <th>Reason</th>
              <th className="num">Diff</th>
            </tr>
          </thead>
          <tbody>
            {items.map((a, i) => {
              const p = a.payload || a;
              const no = a.adjustmentNo || String((p as Record<string, unknown>).adjustmentNo || `adj-${i}`);
              return (
                <tr key={no}>
                  <td>{no}</td>
                  <td>{String((p as Record<string, unknown>).originalBillNo || '—')}</td>
                  <td>{String((p as Record<string, unknown>).reason || '—')}</td>
                  <td className="num">
                    {formatRupee(
                      Number(
                        (p as Record<string, unknown>).diffPayable ??
                          (p as Record<string, unknown>).amount ??
                          0,
                      ),
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
