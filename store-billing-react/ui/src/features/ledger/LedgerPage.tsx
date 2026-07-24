import { useEffect, useState } from 'react';
import { api } from '../../shared/api';
import { useAuth } from '../../shared/auth';
import { formatMoney } from '../../shared/money';

type BillRow = {
  billNo?: string;
  invoiceNo?: string;
  customerName?: string | null;
  payable?: number;
  netAmount?: number;
  paymentMode?: string;
};

export function LedgerPage() {
  const { user } = useAuth();
  const [rows, setRows] = useState<BillRow[]>([]);

  useEffect(() => {
    if (!user?.storeId) return;
    void api<{ items?: BillRow[]; data?: BillRow[] }>(
      `/bills?storeCode=${encodeURIComponent(user.storeId)}&limit=100`,
    ).then((res) => setRows(res.data || res.items || []));
  }, [user?.storeId]);

  return (
    <div>
      <div className="page-header">
        <div>
          <h1>Ledger</h1>
          <p>Recent bills and payments for this store.</p>
        </div>
      </div>
      <div className="panel">
        <table className="table">
          <thead>
            <tr>
              <th>Bill</th>
              <th>Customer</th>
              <th>Mode</th>
              <th>Payable</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.billNo || r.invoiceNo}>
                <td className="linkish">{r.billNo || r.invoiceNo}</td>
                <td>{r.customerName || '—'}</td>
                <td>{r.paymentMode || '—'}</td>
                <td>{formatMoney(Number(r.netAmount ?? r.payable ?? 0))}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
