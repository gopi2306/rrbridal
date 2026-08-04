import { useCallback, useEffect, useState } from 'react';
import { useAuth } from '../../shared/auth';
import { formatRupee, todayIso } from '../../shared/money';
import * as storePos from '../../shared/storePos';
import * as dash from '../../shared/dashboard';
import { Modal } from '../dialogs/Modal';

export function DayClosePage() {
  const { ctx } = useAuth();
  const [session, setSession] = useState<Record<string, unknown> | null>(null);
  const [report, setReport] = useState<Record<string, unknown> | null>(null);
  const [status, setStatus] = useState('');
  const [error, setError] = useState('');
  const [openCash, setOpenCash] = useState('0');
  const [actualCash, setActualCash] = useState('0');
  const [closeOpen, setCloseOpen] = useState(false);
  const businessDate = todayIso();

  const reload = useCallback(async () => {
    setError('');
    try {
      const s = (await storePos.getDaySession(businessDate)) as Record<string, unknown> | null;
      setSession(s);
      const r = await dash.getStoreDayCloseReport(businessDate, ctx?.posCounter);
      setReport(r);
      setStatus(s ? `Session ${(s.status as string) || 'open'}` : 'No open session');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Load failed');
    }
  }, [businessDate, ctx?.posCounter]);

  useEffect(() => {
    void reload();
  }, [reload]);

  const summary = (report?.summary as Record<string, unknown>) || {};
  const expected = Number(summary.expectedCash ?? 0);

  async function openDay() {
    setError('');
    try {
      await storePos.openDaySession({
        businessDate,
        posCounter: ctx?.posCounter,
        status: 'open',
        openingCash: Number(openCash) || 0,
        openedBy: ctx?.userName || ctx?.email || 'cashier',
      });
      setStatus('Day opened.');
      await reload();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Open failed');
    }
  }

  async function closeDay() {
    setError('');
    try {
      await storePos.closeDaySession({
        businessDate,
        posCounter: ctx?.posCounter,
        status: 'closed',
        expectedCash: expected,
        actualCashCounted: Number(actualCash) || 0,
        cashDifference: (Number(actualCash) || 0) - expected,
        closedBy: ctx?.userName || ctx?.email || 'cashier',
      });
      setCloseOpen(false);
      setStatus('Day closed.');
      await reload();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Close failed');
    }
  }

  return (
    <div>
      <h1 className="page-title">Day Close</h1>
      <p className="page-sub">Business date {businessDate} · POS {ctx?.posCounter}</p>
      <div className="kpi-row">
        <div className="kpi">
          <div className="label">Session</div>
          <div className="value">{session ? String(session.status || 'open') : '—'}</div>
        </div>
        <div className="kpi">
          <div className="label">Expected cash</div>
          <div className="value">{formatRupee(expected)}</div>
        </div>
        <div className="kpi">
          <div className="label">Opening cash</div>
          <div className="value">{formatRupee(Number(session?.openingCash ?? summary.openingCash ?? 0))}</div>
        </div>
      </div>
      <div className="card row-actions">
        {!session || session.status === 'closed' ? (
          <>
            <input className="field" style={{ maxWidth: 140 }} value={openCash} onChange={(e) => setOpenCash(e.target.value)} placeholder="Opening cash" />
            <button type="button" className="btn btn-primary" onClick={() => void openDay()}>Open day</button>
          </>
        ) : (
          <button type="button" className="btn btn-primary" onClick={() => { setActualCash(String(expected)); setCloseOpen(true); }}>
            Close day…
          </button>
        )}
        <button type="button" className="btn btn-outline" onClick={() => void reload()}>Refresh</button>
      </div>
      <p className="msg" style={{ marginTop: 10 }}>{status}</p>
      {error ? <p className="msg error">{error}</p> : null}

      {closeOpen ? (
        <Modal
          title="Close day"
          onClose={() => setCloseOpen(false)}
          footer={
            <>
              <button type="button" className="btn btn-outline" onClick={() => setCloseOpen(false)}>Cancel</button>
              <button type="button" className="btn btn-primary" onClick={() => void closeDay()}>Confirm close</button>
            </>
          }
        >
          <p>Expected cash {formatRupee(expected)}</p>
          <label className="form-label">Actual cash counted</label>
          <input className="field" value={actualCash} onChange={(e) => setActualCash(e.target.value)} />
        </Modal>
      ) : null}
    </div>
  );
}
