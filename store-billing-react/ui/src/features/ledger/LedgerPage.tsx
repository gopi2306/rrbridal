import { useCallback, useEffect, useState } from 'react';
import { formatRupee } from '../../shared/money';
import * as storePos from '../../shared/storePos';

export function LedgerPage() {
  const [bills, setBills] = useState<Record<string, unknown>[]>([]);
  const [payments, setPayments] = useState<Record<string, unknown>[]>([]);
  const [status, setStatus] = useState('Ledger of recent bills and gateway payments.');
  const [error, setError] = useState('');

  const reload = useCallback(async () => {
    setError('');
    try {
      const [b, g] = await Promise.all([
        storePos.listBills(undefined, 80),
        storePos.listGatewayPayments(100),
      ]);
      setBills(Array.isArray(b) ? (b as Record<string, unknown>[]) : []);
      setPayments(Array.isArray(g) ? (g as Record<string, unknown>[]) : []);
      setStatus('Ledger refreshed.');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Load failed');
    }
  }, []);

  useEffect(() => {
    void reload();
  }, [reload]);

  return (
    <div>
      <h1 className="page-title">Ledger</h1>
      <p className="page-sub">Posted bills and gateway payment settlements.</p>

      <div className="row-actions" style={{ marginBottom: 12 }}>
        <button type="button" className="btn btn-outline" onClick={() => void reload()}>
          Refresh
        </button>
      </div>

      <p className="msg">{status}</p>
      {error ? <p className="msg error">{error}</p> : null}

      <div className="section-title">Bills</div>
      <div className="card" style={{ padding: 0, overflow: 'auto', marginBottom: 16 }}>
        <table className="data">
          <thead>
            <tr>
              <th>Bill</th>
              <th>Customer</th>
              <th>Channel</th>
              <th className="num">Payable</th>
              <th className="num">Balance</th>
            </tr>
          </thead>
          <tbody>
            {bills.map((r) => {
              const p = (r.payload as Record<string, unknown>) || {};
              const cb = p.creditBilling as Record<string, unknown> | undefined;
              return (
                <tr key={String(r.billNo)}>
                  <td>{String(r.billNo)}</td>
                  <td>{String(p.customerName || '—')}</td>
                  <td>{String(p.salesChannel || 'store')}</td>
                  <td className="num">
                    {formatRupee(Number(p.payable ?? (p.totals as Record<string, unknown> | undefined)?.payable ?? 0))}
                  </td>
                  <td className="num">{formatRupee(Number(p.balanceDue ?? cb?.balanceDue ?? 0))}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      <div className="section-title">Gateway payments</div>
      <div className="card" style={{ padding: 0, overflow: 'auto' }}>
        <table className="data">
          <thead>
            <tr>
              <th>Ref / txn</th>
              <th>Bill</th>
              <th>Provider</th>
              <th>Mode</th>
              <th className="num">Amount</th>
            </tr>
          </thead>
          <tbody>
            {payments.map((r, i) => {
              const p = (r.payload as Record<string, unknown>) || r;
              return (
                <tr key={String(p.txnId || p.reference || p._id || i)}>
                  <td>{String(p.txnId || p.reference || p.paymentRef || '—')}</td>
                  <td>{String(p.billNo || p.invoiceNo || '—')}</td>
                  <td>{String(p.provider || p.gateway || '—')}</td>
                  <td>{String(p.paymentMode || p.mode || '—')}</td>
                  <td className="num">{formatRupee(Number(p.amount ?? 0))}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
