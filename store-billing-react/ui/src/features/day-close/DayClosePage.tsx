import type { FormEvent } from 'react';
import { useEffect, useState } from 'react';
import { api } from '../../shared/api';
import { useAuth } from '../../shared/auth';

export function DayClosePage() {
  const { user } = useAuth();
  const storeId = user?.storeId || '';
  const [openingCash, setOpeningCash] = useState('0');
  const [sessions, setSessions] = useState<unknown[]>([]);
  const [movements, setMovements] = useState<unknown[]>([]);
  const [message, setMessage] = useState('');
  const today = new Date().toISOString().slice(0, 10);

  async function refresh() {
    const [s, m] = await Promise.all([
      api<{ items: unknown[] }>(`/day-sessions?storeId=${encodeURIComponent(storeId)}`),
      api<{ items: unknown[] }>(`/cash-movements?storeId=${encodeURIComponent(storeId)}`),
    ]);
    setSessions(s.items || []);
    setMovements(m.items || []);
  }

  useEffect(() => {
    if (storeId) void refresh().catch((e: Error) => setMessage(e.message));
  }, [storeId]);

  async function openDay(e: FormEvent) {
    e.preventDefault();
    try {
      await api('/day-sessions/open', {
        method: 'POST',
        body: JSON.stringify({
          storeId,
          businessDate: today,
          posCounter: user?.posCounter || '1',
          openingCash: Number(openingCash) || 0,
          status: 'open',
        }),
      });
      setMessage('Day opened');
      await refresh();
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Failed');
    }
  }

  async function closeDay() {
    try {
      await api('/day-sessions/close', {
        method: 'POST',
        body: JSON.stringify({
          storeId,
          businessDate: today,
          posCounter: user?.posCounter || '1',
          status: 'closed',
        }),
      });
      setMessage('Day closed');
      await refresh();
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Failed');
    }
  }

  async function cashMove(kind: 'deposit' | 'withdraw') {
    const amount = prompt(`Amount to ${kind}?`);
    if (!amount) return;
    try {
      await api('/cash-movements', {
        method: 'POST',
        body: JSON.stringify({
          storeId,
          businessDate: today,
          kind,
          amount: Number(amount),
          movementNo: `CM-${Date.now()}`,
        }),
      });
      await refresh();
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Failed');
    }
  }

  return (
    <div>
      <div className="page-header">
        <div>
          <h1>Day Close</h1>
          <p>Open/close the business day and record cash movements.</p>
        </div>
      </div>
      <form className="form-card" onSubmit={(e) => void openDay(e)}>
        <div className="grid-2">
          <div className="field">
            <label>Business date</label>
            <input value={today} readOnly />
          </div>
          <div className="field">
            <label>Opening cash</label>
            <input value={openingCash} onChange={(e) => setOpeningCash(e.target.value)} type="number" />
          </div>
        </div>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          <button className="btn btn-primary" type="submit">
            Open day
          </button>
          <button className="btn btn-secondary" type="button" onClick={() => void closeDay()}>
            Close day
          </button>
          <button className="btn btn-secondary" type="button" onClick={() => void cashMove('deposit')}>
            Cash deposit
          </button>
          <button className="btn btn-secondary" type="button" onClick={() => void cashMove('withdraw')}>
            Cash withdraw
          </button>
        </div>
        {message && <p className="muted">{message}</p>}
      </form>
      <div className="grid-2">
        <div className="panel" style={{ padding: 12 }}>
          <strong>Sessions</strong>
          <pre style={{ fontSize: 12 }}>{JSON.stringify(sessions, null, 2)}</pre>
        </div>
        <div className="panel" style={{ padding: 12 }}>
          <strong>Cash movements</strong>
          <pre style={{ fontSize: 12 }}>{JSON.stringify(movements, null, 2)}</pre>
        </div>
      </div>
    </div>
  );
}
