import type { FormEvent } from 'react';
import { useEffect, useState } from 'react';
import { api } from '../../shared/api';
import { useAuth } from '../../shared/auth';
import { formatMoney } from '../../shared/money';

export function ExpensesPage() {
  const { user } = useAuth();
  const storeId = user?.storeId || '';
  const [items, setItems] = useState<Array<{ payload?: { description?: string; amount?: number }; expenseNo?: string }>>([]);
  const [description, setDescription] = useState('');
  const [amount, setAmount] = useState('');
  const [message, setMessage] = useState('');

  async function load() {
    const res = await api<{ items: typeof items }>(`/expenses?storeId=${encodeURIComponent(storeId)}`);
    setItems(res.items || []);
  }

  useEffect(() => {
    if (storeId) void load().catch((e: Error) => setMessage(e.message));
  }, [storeId]);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    try {
      await api('/expenses', {
        method: 'POST',
        body: JSON.stringify({
          storeId,
          expenseNo: `EX-${Date.now()}`,
          description,
          amount: Number(amount),
          businessDate: new Date().toISOString().slice(0, 10),
        }),
      });
      setDescription('');
      setAmount('');
      setMessage('Expense posted');
      await load();
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Failed');
    }
  }

  return (
    <div>
      <div className="page-header">
        <div>
          <h1>Daily Expenses</h1>
          <p>Record day expenses against the open business date.</p>
        </div>
      </div>
      <form className="form-card" onSubmit={(e) => void onSubmit(e)} style={{ maxWidth: 480 }}>
        <div className="field">
          <label>Description</label>
          <input required value={description} onChange={(e) => setDescription(e.target.value)} />
        </div>
        <div className="field">
          <label>Amount</label>
          <input required type="number" value={amount} onChange={(e) => setAmount(e.target.value)} />
        </div>
        <button className="btn btn-primary" type="submit">
          Post expense
        </button>
        {message && <p className="muted">{message}</p>}
      </form>
      <div className="panel">
        <table className="table">
          <thead>
            <tr>
              <th>No</th>
              <th>Description</th>
              <th>Amount</th>
            </tr>
          </thead>
          <tbody>
            {items.map((ex, i) => (
              <tr key={i}>
                <td>{ex.expenseNo || (ex.payload as { expenseNo?: string } | undefined)?.expenseNo || '—'}</td>
                <td>{ex.payload?.description || '—'}</td>
                <td>{formatMoney(Number(ex.payload?.amount ?? 0))}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
