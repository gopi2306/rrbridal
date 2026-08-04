import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Modal } from '../dialogs/Modal';
import { useAuth } from '../../shared/auth';
import { formatRupee, todayIso } from '../../shared/money';
import * as storePos from '../../shared/storePos';

const TYPES = [
  { key: '1', title: 'Sales', subtitle: 'Opens Billing — post a store sale invoice', path: '/' },
  { key: '2', title: 'Receipt', subtitle: 'Opens Credit Bills — collect customer AR (party receipt)', path: '/credit-bills' },
  { key: '3', title: 'Payment', subtitle: 'Cash withdrawal voucher — post via cash movement', path: null },
  { key: '4', title: 'Credit Note', subtitle: 'Bill return or direct customer credit (no bill)', path: null },
  { key: '5', title: 'Journal', subtitle: 'Opens Adjustments — bill value / stock journal-style adjust', path: '/adjustments' },
  { key: '6', title: 'Daily Expense', subtitle: 'Opens Expenses — post a daily expense slip', path: '/expenses' },
] as const;

export function VouchersPage() {
  const navigate = useNavigate();
  const { ctx } = useAuth();
  const [selected, setSelected] = useState(0);
  const [status, setStatus] = useState('Pick a type and press Enter (or 1–6)');
  const [cashOpen, setCashOpen] = useState(false);
  const [cnOpen, setCnOpen] = useState(false);
  const [amount, setAmount] = useState('');
  const [notes, setNotes] = useState('');
  const [cnPhone, setCnPhone] = useState('');
  const [cnAmount, setCnAmount] = useState('');
  const [error, setError] = useState('');

  function openSelected() {
    const t = TYPES[selected];
    if (!t) return;
    if (t.path) {
      navigate(t.path);
      setStatus(`Opened ${t.title}.`);
      return;
    }
    if (t.key === '3') setCashOpen(true);
    if (t.key === '4') setCnOpen(true);
  }

  async function postCashOut() {
    setError('');
    try {
      const no = await storePos.nextNumber('cashMovementNo');
      await storePos.postCashMovement({
        cashMovementNo: no,
        businessDate: todayIso(),
        posCounter: ctx?.posCounter,
        direction: 'out',
        amount: Number(amount) || 0,
        notes,
        createdBy: ctx?.userName || ctx?.email,
      });
      setCashOpen(false);
      setStatus(`Cash payment ${no} posted.`);
      setAmount('');
      setNotes('');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed');
    }
  }

  async function postCn() {
    setError('');
    try {
      const no = await storePos.nextNumber('creditNoteNo');
      await storePos.postCreditNote({
        creditNoteNo: no,
        businessDate: todayIso(),
        customerPhone: cnPhone,
        amount: Number(cnAmount) || 0,
        remainingAmount: Number(cnAmount) || 0,
        reason: 'Direct customer credit',
        storeId: ctx?.storeId,
      });
      setCnOpen(false);
      setStatus(`Credit note ${no} created.`);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed');
    }
  }

  return (
    <div>
      <h1 className="page-title">Accounting Vouchers</h1>
      <p className="page-sub">Tally-style create · pick a type and press Enter (or 1–6)</p>
      <div className="card" style={{ padding: 0 }}>
        <ul className="voucher-list">
          {TYPES.map((t, i) => (
            <li
              key={t.key}
              className={`voucher-item${selected === i ? ' selected' : ''}`}
              onClick={() => setSelected(i)}
              onDoubleClick={openSelected}
            >
              <div className="voucher-key">{t.key}</div>
              <div>
                <div style={{ fontWeight: 600, fontSize: 15 }}>{t.title}</div>
                <div className="msg">{t.subtitle}</div>
              </div>
            </li>
          ))}
        </ul>
        <div className="modal-f" style={{ justifyContent: 'space-between' }}>
          <span className="msg">{status}</span>
          <div className="row-actions">
            <button type="button" className="btn btn-outline" onClick={() => navigate('/online-sales')}>
              COD Receipt…
            </button>
            <button type="button" className="btn btn-primary" onClick={openSelected}>
              Create (Enter)
            </button>
          </div>
        </div>
      </div>
      {error ? <p className="msg error" style={{ marginTop: 10 }}>{error}</p> : null}

      {cashOpen ? (
        <Modal
          title="Cash payment (withdrawal)"
          onClose={() => setCashOpen(false)}
          footer={
            <>
              <button type="button" className="btn btn-outline" onClick={() => setCashOpen(false)}>Cancel</button>
              <button type="button" className="btn btn-primary" onClick={() => void postCashOut()}>Post</button>
            </>
          }
        >
          <label className="form-label">Amount</label>
          <input className="field" value={amount} onChange={(e) => setAmount(e.target.value)} />
          <label className="form-label" style={{ marginTop: 8, display: 'block' }}>Notes</label>
          <input className="field" value={notes} onChange={(e) => setNotes(e.target.value)} />
        </Modal>
      ) : null}

      {cnOpen ? (
        <Modal
          title="Direct customer credit note"
          onClose={() => setCnOpen(false)}
          footer={
            <>
              <button type="button" className="btn btn-outline" onClick={() => setCnOpen(false)}>Cancel</button>
              <button type="button" className="btn btn-primary" onClick={() => void postCn()}>Create</button>
            </>
          }
        >
          <label className="form-label">Customer phone</label>
          <input className="field" value={cnPhone} onChange={(e) => setCnPhone(e.target.value)} />
          <label className="form-label" style={{ marginTop: 8, display: 'block' }}>Amount ({formatRupee(Number(cnAmount) || 0)})</label>
          <input className="field" value={cnAmount} onChange={(e) => setCnAmount(e.target.value)} />
        </Modal>
      ) : null}
    </div>
  );
}
