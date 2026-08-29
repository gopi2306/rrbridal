/**
 * Expected cash formula and summary section sign for withdrawals.
 * Matches docs/day-close.md and WPF DaySessionCashMath.
 */
import assert from 'node:assert/strict';
import { buildSummaryRows } from '../dashboard/store-day-close-report-sections';
import type { StoreDayCloseReportData } from '../dashboard/store-day-close-report.types';

function computeExpectedCash(
  openingCash: number,
  netCashInHand: number,
  depositsTotal: number,
  withdrawalsTotal: number,
): number {
  return openingCash + netCashInHand - depositsTotal - withdrawalsTotal;
}

function run() {
  assert.equal(computeExpectedCash(5000, 45100, 1000, 200), 48900);
  assert.equal(computeExpectedCash(0, 69300, 0, 0), 69300);

  const summaryRows = buildSummaryRows({
    store: { code: 'store-1', name: 'Test' },
    businessDate: '2026-08-27',
    counterScope: 'All counters',
    sessionStatus: 'open',
    exportedAt: new Date().toISOString(),
    summary: {
      openingCash: 0,
      cashTotal: 70000,
      returnCashRefundTotal: 0,
      creditNoteCashoutTotal: 0,
      dailyExpensesTotal: 1000,
      dailyExpenseCashTotal: 700,
      depositsTotal: 500,
      withdrawalsTotal: 200,
      expectedCash: 68600,
      actualCashCounted: 0,
      cashDifference: 0,
      netCashInHand: 69300,
      netCardInHand: 0,
      netUpiInHand: 0,
      actualHandInTotal: 69300,
      billCount: 16,
      returnCount: 0,
      cardTotal: 0,
      upiTotal: 0,
      creditNoteTotal: 0,
      returnTotalAmount: 0,
      creditNoteIssuedTotal: 0,
    },
    counterRollup: [],
    bills: [],
    returns: [],
    adjustments: [],
    expenses: [],
    cashMovements: [],
    creditNoteCashouts: [],
    denominations: [],
  } satisfies StoreDayCloseReportData);

  const withdrawalsRow = summaryRows.find((r) => r.label === 'Cash withdrawals');
  assert.ok(withdrawalsRow);
  assert.match(withdrawalsRow!.value, /^-/);

  console.log('day-close-expected-cash.selftest: ok');
}

run();
