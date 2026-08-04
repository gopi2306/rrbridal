import { useCallback, useEffect, useState } from 'react';
import { ApiError } from '../../shared/api';
import { useAuth } from '../../shared/auth';
import { formatRupee, round2 } from '../../shared/money';
import * as storePos from '../../shared/storePos';
import { Modal } from '../dialogs/Modal';

type BillRow = {
  billNo: string;
  payload?: Record<string, unknown>;
};

function isPendingCod(p: Record<string, unknown>) {
  const oc = p.onlineCod as Record<string, unknown> | undefined;
  const channel = String(p.salesChannel || '').toLowerCase();
  const status = String(oc?.status || '').toLowerCase();
  return (channel === 'online' || !!oc) && (status === 'pending' || status === '');
}

function amountDue(p: Record<string, unknown>) {
  const oc = p.onlineCod as Record<string, unknown> | undefined;
  return Number(
    oc?.amountDue ?? oc?.amount ?? p.payable ?? (p.totals as Record<string, unknown> | undefined)?.payable ?? 0,
  );
}

export function OnlineSalesPage() {
  const { ctx } = useAuth();
  const [items, setItems] = useState<BillRow[]>([]);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('Search online COD orders.');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [payBill, setPayBill] = useState<BillRow | null>(null);
  const [mode, setMode] = useState('Cash');
  const [txnNo, setTxnNo] = useState('');
  const [amount, setAmount] = useState('');

  const reload = useCallback(async () => {
    setError('');
    setBusy(true);
    try {
      const rows = (await storePos.listBills(search.trim() || undefined, 120)) as BillRow[];
      const pending = (Array.isArray(rows) ? rows : []).filter((r) => isPendingCod(r.payload || {}));
      setItems(pending);
      const bal = pending.reduce((s, r) => s + amountDue(r.payload || {}), 0);
      setStatus(
        pending.length
          ? `${pending.length} pending COD · balance till ${formatRupee(bal)}`
          : 'No online COD orders found.',
      );
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Search failed');
    } finally {
      setBusy(false);
    }
  }, [search]);

  useEffect(() => {
    void reload();
  }, [reload]);

  function openPay(row: BillRow) {
    setPayBill(row);
    setMode('Cash');
    setTxnNo('');
    setAmount(String(amountDue(row.payload || {})));
  }

  async function recordPayment() {
    if (!payBill) return;
    setError('');
    setBusy(true);
    try {
      const p = payBill.payload || {};
      const due = amountDue(p);
      const paid = round2(Number(amount) || due);
      if (!txnNo.trim() && mode !== 'Cash') throw new Error('Enter transaction / reference number.');
      const ref = txnNo.trim() || `CASH-${Date.now()}`;
      const provider = mode === 'Card' ? 'PineLabs' : mode === 'UPI' ? 'Razorpay' : 'Cash';
      await storePos.postCodPayment(payBill.billNo, {
        billNo: payBill.billNo,
        storeId: ctx?.storeId,
        salesChannel: 'online',
        payable: due,
        paymentMode: mode,
        payments: [{ provider, amount: paid, reference: ref, status: 'posted' }],
        onlineCod: {
          status: 'received',
          amount: paid,
          amountDue: 0,
          transactionNo: ref,
          receivedBy: ctx?.userName || ctx?.email || 'cashier',
          receivedPaymentMode: mode,
          receivedAtUtc: new Date().toISOString(),
        },
      });
      setPayBill(null);
      setStatus(`Payment recorded for ${payBill.billNo}.`);
      await reload();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : e instanceof Error ? e.message : 'Payment failed');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <h1 className="page-title">Online sales</h1>
      <p className="page-sub">Track online COD invoices and record collections.</p>

      <div className="card row-actions" style={{ marginBottom: 12 }}>
        <input
          className="field"
          style={{ flex: 1 }}
          placeholder="Bill no / customer"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') void reload();
          }}
        />
        <button type="button" className="btn btn-outline" disabled={busy} onClick={() => void reload()}>
          Refresh
        </button>
      </div>

      <p className="msg">{status}</p>
      {error ? <p className="msg error">{error}</p> : null}

      <div className="card" style={{ padding: 0, overflow: 'auto', marginTop: 12 }}>
        <table className="data">
          <thead>
            <tr>
              <th>Bill</th>
              <th>Customer</th>
              <th>Status</th>
              <th className="num">Amount due</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {items.map((r) => {
              const p = r.payload || {};
              return (
                <tr key={r.billNo}>
                  <td>{r.billNo}</td>
                  <td>{String(p.customerName || '—')}</td>
                  <td>{String((p.onlineCod as Record<string, unknown> | undefined)?.status || 'pending')}</td>
                  <td className="num">{formatRupee(amountDue(p))}</td>
                  <td>
                    <button type="button" className="btn btn-primary btn-sm" onClick={() => openPay(r)}>
                      Record payment
                    </button>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {payBill ? (
        <Modal
          title={`COD payment — ${payBill.billNo}`}
          onClose={() => setPayBill(null)}
          footer={
            <>
              <button type="button" className="btn btn-outline" onClick={() => setPayBill(null)}>
                Cancel
              </button>
              <button type="button" className="btn btn-primary" disabled={busy} onClick={() => void recordPayment()}>
                Record payment
              </button>
            </>
          }
        >
          <label className="form-label">Payment mode</label>
          <select className="field" value={mode} onChange={(e) => setMode(e.target.value)}>
            <option>Cash</option>
            <option>UPI</option>
            <option>Card</option>
          </select>
          <label className="form-label" style={{ marginTop: 8, display: 'block' }}>
            Transaction / reference
          </label>
          <input className="field" value={txnNo} onChange={(e) => setTxnNo(e.target.value)} />
          <label className="form-label" style={{ marginTop: 8, display: 'block' }}>
            Amount ({formatRupee(Number(amount) || 0)})
          </label>
          <input className="field" value={amount} onChange={(e) => setAmount(e.target.value)} />
        </Modal>
      ) : null}
    </div>
  );
}
