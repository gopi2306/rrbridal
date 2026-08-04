import { useCallback, useEffect, useState } from 'react';
import { ApiError } from '../../shared/api';
import { useAuth } from '../../shared/auth';
import { formatRupee, round2, todayIso } from '../../shared/money';
import * as storePos from '../../shared/storePos';

type ExpenseRow = {
  expenseNo?: string;
  payload?: Record<string, unknown>;
  createdAt?: string;
};

export function ExpensesPage() {
  const { ctx } = useAuth();
  const [businessDate, setBusinessDate] = useState(todayIso());
  const [description, setDescription] = useState('');
  const [amount, setAmount] = useState('');
  const [items, setItems] = useState<ExpenseRow[]>([]);
  const [status, setStatus] = useState('Enter a description for the expense.');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const reload = useCallback(async () => {
    setError('');
    try {
      const rows = (await storePos.listDailyExpenses(businessDate, 100)) as ExpenseRow[];
      setItems(Array.isArray(rows) ? rows : []);
      const total = (Array.isArray(rows) ? rows : []).reduce((s, r) => {
        const p = (r.payload as Record<string, unknown>) || r;
        return s + Number((p as Record<string, unknown>).amount ?? 0);
      }, 0);
      setStatus(
        rows?.length
          ? `${rows.length} expense(s) · day total ${formatRupee(total)}`
          : 'No expenses for this date.',
      );
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Load failed');
    }
  }, [businessDate]);

  useEffect(() => {
    void reload();
  }, [reload]);

  async function postExpense() {
    setError('');
    setBusy(true);
    try {
      if (!description.trim()) throw new Error('Enter a description for the expense.');
      const amt = round2(Number(amount) || 0);
      if (amt <= 0) throw new Error('Amount must be greater than zero.');
      const expenseNo = await storePos.nextNumber('expenseNo');
      await storePos.postDailyExpense({
        expenseNo,
        storeId: ctx?.storeId,
        deviceId: ctx?.deviceId,
        posCounter: ctx?.posCounter,
        businessDate,
        description: description.trim(),
        amount: amt,
        status: 'posted',
        createdBy: ctx?.userName || ctx?.email,
      });
      setStatus(`Posted ${expenseNo} — ${formatRupee(amt)}.`);
      setDescription('');
      setAmount('');
      await reload();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : e instanceof Error ? e.message : 'Post failed');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <h1 className="page-title">Daily expense</h1>
      <p className="page-sub">Post a daily expense slip for the business date.</p>

      <div className="card" style={{ marginBottom: 12 }}>
        <label className="form-label">Business date</label>
        <input
          className="field"
          type="date"
          value={businessDate}
          onChange={(e) => setBusinessDate(e.target.value)}
        />
        <label className="form-label" style={{ marginTop: 8, display: 'block' }}>Description</label>
        <input className="field" value={description} onChange={(e) => setDescription(e.target.value)} />
        <label className="form-label" style={{ marginTop: 8, display: 'block' }}>Amount</label>
        <input className="field" value={amount} onChange={(e) => setAmount(e.target.value)} />
        <div className="row-actions" style={{ marginTop: 10 }}>
          <button type="button" className="btn btn-primary" disabled={busy} onClick={() => void postExpense()}>
            Post expense
          </button>
          <button type="button" className="btn btn-outline" onClick={() => void reload()}>
            Refresh
          </button>
        </div>
      </div>

      <p className="msg">{status}</p>
      {error ? <p className="msg error">{error}</p> : null}

      <div className="card" style={{ padding: 0, overflow: 'auto', marginTop: 12 }}>
        <table className="data">
          <thead>
            <tr>
              <th>Expense no</th>
              <th>Description</th>
              <th className="num">Amount</th>
            </tr>
          </thead>
          <tbody>
            {items.map((r, i) => {
              const p = (r.payload as Record<string, unknown>) || r;
              const no = r.expenseNo || String((p as Record<string, unknown>).expenseNo || `exp-${i}`);
              return (
                <tr key={no}>
                  <td>{no}</td>
                  <td>{String((p as Record<string, unknown>).description || '—')}</td>
                  <td className="num">{formatRupee(Number((p as Record<string, unknown>).amount ?? 0))}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
