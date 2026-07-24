import { useEffect, useState } from 'react';
import { api } from '../../shared/api';
import { useAuth } from '../../shared/auth';
import { formatMoney } from '../../shared/money';

type CreditRow = {
  billNo: string;
  balance: number;
  payload: Record<string, unknown>;
};

export function CreditBillsPage() {
  const { user } = useAuth();
  const storeId = user?.storeId || '';
  const [items, setItems] = useState<CreditRow[]>([]);
  const [status, setStatus] = useState('pending');
  const [message, setMessage] = useState('');

  async function load() {
    const res = await api<{ items: CreditRow[] }>(
      `/credit-bills?storeId=${encodeURIComponent(storeId)}&status=${status}`,
    );
    setItems(res.items || []);
  }

  useEffect(() => {
    if (storeId) void load().catch((e: Error) => setMessage(e.message));
  }, [storeId, status]);

  async function collect(billNo: string, balance: number) {
    const amount = prompt(`Collect amount (balance ${balance})`, String(balance));
    if (!amount) return;
    try {
      await api('/payments/credit', {
        method: 'POST',
        body: JSON.stringify({
          storeId,
          billNo,
          amount: Number(amount),
          paymentMode: 'cash',
        }),
      });
      setMessage(`Payment recorded for ${billNo}`);
      await load();
    } catch (e) {
      setMessage(e instanceof Error ? e.message : 'Failed');
    }
  }

  return (
    <div>
      <div className="page-header">
        <div>
          <h1>Credit Bills</h1>
          <p>Collect outstanding credit sale balances.</p>
        </div>
        <div className="seg">
          {['pending', 'settled', 'all'].map((s) => (
            <button key={s} type="button" className={status === s ? 'active' : ''} onClick={() => setStatus(s)}>
              {s}
            </button>
          ))}
        </div>
      </div>
      {message && <p className="muted">{message}</p>}
      <div className="panel">
        <table className="table">
          <thead>
            <tr>
              <th>Bill</th>
              <th>Customer</th>
              <th>Balance</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {items.map((r) => (
              <tr key={r.billNo}>
                <td className="linkish">{r.billNo}</td>
                <td>{String(r.payload.customerName || '—')}</td>
                <td>{formatMoney(r.balance)}</td>
                <td>
                  {r.balance > 0 && (
                    <button className="btn btn-primary" type="button" onClick={() => void collect(r.billNo, r.balance)}>
                      Collect
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {!items.length && <div className="empty">No credit bills</div>}
      </div>
    </div>
  );
}
