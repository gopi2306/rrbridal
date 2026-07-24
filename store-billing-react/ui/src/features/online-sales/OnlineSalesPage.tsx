import { useEffect, useState } from 'react';
import { api } from '../../shared/api';
import { useAuth } from '../../shared/auth';
import { formatMoney } from '../../shared/money';

type OnlineRow = {
  billNo: string;
  payload: Record<string, unknown>;
};

export function OnlineSalesPage() {
  const { user } = useAuth();
  const storeId = user?.storeId || '';
  const [items, setItems] = useState<OnlineRow[]>([]);
  const [message, setMessage] = useState('');

  async function load() {
    const res = await api<{ items: OnlineRow[] }>(`/online-sales?storeId=${encodeURIComponent(storeId)}`);
    setItems(res.items || []);
  }

  useEffect(() => {
    if (storeId) void load().catch((e: Error) => setMessage(e.message));
  }, [storeId]);

  async function recordPayment(billNo: string) {
    const txn = prompt('Transaction / reference number');
    const amount = prompt('Amount received');
    if (!txn || !amount) return;
    try {
      await api('/payments/cod', {
        method: 'POST',
        body: JSON.stringify({
          storeId,
          billNo,
          amount: Number(amount),
          txnNo: txn,
          paymentMode: 'upi',
        }),
      });
      setMessage(`COD payment recorded for ${billNo}`);
      await load();
    } catch (e) {
      setMessage(e instanceof Error ? e.message : 'Failed');
    }
  }

  return (
    <div>
      <div className="page-header">
        <div>
          <h1>Online Sales</h1>
          <p>Track online COD invoices and record collections.</p>
        </div>
      </div>
      {message && <p className="muted">{message}</p>}
      <div className="panel">
        <table className="table">
          <thead>
            <tr>
              <th>Bill</th>
              <th>Customer</th>
              <th>Payable</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {items.map((r) => (
              <tr key={r.billNo}>
                <td className="linkish">{r.billNo}</td>
                <td>{String(r.payload.customerName || '—')}</td>
                <td>{formatMoney(Number(r.payload.payable ?? 0))}</td>
                <td>
                  <button className="btn btn-primary" type="button" onClick={() => void recordPayment(r.billNo)}>
                    Record payment
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {!items.length && <div className="empty">No online COD bills</div>}
      </div>
    </div>
  );
}
