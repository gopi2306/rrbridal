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

function balanceDueOf(p: Record<string, unknown>) {
  const cb = p.creditBilling as Record<string, unknown> | undefined;
  return Number(p.balanceDue ?? cb?.balanceDue ?? 0);
}

function isCreditOpen(p: Record<string, unknown>) {
  const bal = balanceDueOf(p);
  return !!p.billOnCredit || bal > 0.009 || String(cbStatus(p)).toLowerCase() === 'pending' || String(cbStatus(p)).toLowerCase() === 'partial';
}

function cbStatus(p: Record<string, unknown>) {
  const cb = p.creditBilling as Record<string, unknown> | undefined;
  return String(cb?.status || (balanceDueOf(p) > 0.009 ? 'pending' : ''));
}

export function CreditBillsPage() {
  const { ctx } = useAuth();
  const [items, setItems] = useState<BillRow[]>([]);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('Search credit (pay-later) bills.');
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
      const rows = (await storePos.listBills(search.trim() || undefined, 150)) as BillRow[];
      const credit = (Array.isArray(rows) ? rows : []).filter((r) => isCreditOpen(r.payload || {}));
      setItems(credit);
      const bal = credit.reduce((s, r) => s + balanceDueOf(r.payload || {}), 0);
      setStatus(
        credit.length
          ? `${credit.length} open credit bill(s) · ${formatRupee(bal)} pending`
          : 'No credit bills found.',
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
    setAmount(String(balanceDueOf(row.payload || {})));
  }

  async function recordPayment() {
    if (!payBill) return;
    setError('');
    setBusy(true);
    try {
      const p = payBill.payload || {};
      const due = balanceDueOf(p);
      const paid = round2(Number(amount) || 0);
      if (paid <= 0) throw new Error('Enter amount received.');
      if (paid > due + 0.01) throw new Error('Amount exceeds balance due.');
      const ref = txnNo.trim() || `CR-${Date.now()}`;
      const receiptNo = await storePos.nextNumber('paymentReceiptNo');
      const newBalance = round2(due - paid);
      const payable = Number(p.payable ?? (p.totals as Record<string, unknown> | undefined)?.payable ?? due);
      await storePos.postCreditPayment(payBill.billNo, {
        billNo: payBill.billNo,
        storeId: ctx?.storeId,
        customerName: p.customerName,
        customerPhone: p.customerPhone,
        payable,
        paymentMode: mode,
        payments: [{ provider: mode, amount: paid, reference: ref, status: 'posted' }],
        creditBilling: {
          balanceDue: newBalance,
          status: newBalance <= 0.009 ? 'settled' : 'partial',
          amountPaid: paid,
        },
        receipt: {
          receiptNo,
          billNo: payBill.billNo,
          amount: paid,
          paymentMode: mode,
          reference: ref,
          receivedBy: ctx?.userName || ctx?.email || 'cashier',
        },
      });
      setPayBill(null);
      setStatus(`Receipt saved for ${payBill.billNo}. Receipt no. ${receiptNo}.`);
      await reload();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : e instanceof Error ? e.message : 'Payment failed');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <h1 className="page-title">Credit bills</h1>
      <p className="page-sub">Collect customer AR (party receipt) against pay-later invoices.</p>

      <div className="card row-actions" style={{ marginBottom: 12 }}>
        <input
          className="field"
          style={{ flex: 1 }}
          placeholder="Bill no / customer / phone"
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
              <th>Phone</th>
              <th>Status</th>
              <th className="num">Balance due</th>
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
                  <td>{String(p.customerPhone || '—')}</td>
                  <td>{cbStatus(p) || 'open'}</td>
                  <td className="num">{formatRupee(balanceDueOf(p))}</td>
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
          title={`Credit payment — ${payBill.billNo}`}
          onClose={() => setPayBill(null)}
          footer={
            <>
              <button type="button" className="btn btn-outline" onClick={() => setPayBill(null)}>
                Cancel
              </button>
              <button type="button" className="btn btn-primary" disabled={busy} onClick={() => void recordPayment()}>
                Save receipt
              </button>
            </>
          }
        >
          <p className="msg">
            Balance due {formatRupee(balanceDueOf(payBill.payload || {}))}
          </p>
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
            Amount received
          </label>
          <input className="field" value={amount} onChange={(e) => setAmount(e.target.value)} />
        </Modal>
      ) : null}
    </div>
  );
}
