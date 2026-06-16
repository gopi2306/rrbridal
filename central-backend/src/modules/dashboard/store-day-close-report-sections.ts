import type { StoreDayCloseReportData } from './store-day-close-report.types';

export type KeyValueRow = { label: string; value: string };
export type TableSection = { name: string; headers: string[]; rows: string[][] };

function cell(value?: string): string {
  return value ?? '';
}

export function formatMoney(value: number): string {
  if (!Number.isFinite(value)) return '';
  return value.toFixed(2);
}

export function escapeCsv(value: string): string {
  const s = value ?? '';
  if (s.includes('"') || s.includes(',') || s.includes('\n') || s.includes('\r')) {
    return `"${s.replace(/"/g, '""')}"`;
  }
  return s;
}

export function buildMetadataRows(data: StoreDayCloseReportData): KeyValueRow[] {
  return [
    { label: 'Report', value: 'Day Close Full Report' },
    { label: 'Store', value: `${data.store.name} (${data.store.code})` },
    { label: 'Business date', value: data.businessDate },
    { label: 'Counter scope', value: data.counterScope },
    { label: 'Session status', value: data.sessionStatus },
    { label: 'Exported at', value: data.exportedAt },
    { label: 'Time zone', value: 'Asia/Kolkata' },
  ];
}

export function buildSummaryRows(data: StoreDayCloseReportData): KeyValueRow[] {
  const s = data.summary;
  return [
    { label: 'Opening cash', value: formatMoney(s.openingCash) },
    { label: 'Gross cash from bills', value: formatMoney(s.cashTotal) },
    { label: 'Cash refunds (returns)', value: formatMoney(-s.returnCashRefundTotal) },
    { label: 'Credit note cashouts', value: formatMoney(-s.creditNoteCashoutTotal) },
    { label: 'Daily expenses', value: formatMoney(-s.dailyExpensesTotal) },
    { label: 'Deposits to bank', value: formatMoney(-s.depositsTotal) },
    { label: 'Cash withdrawals', value: formatMoney(s.withdrawalsTotal) },
    { label: 'Expected cash (drawer)', value: formatMoney(s.expectedCash) },
    { label: 'Actual cash counted', value: formatMoney(s.actualCashCounted) },
    { label: 'Difference', value: formatMoney(s.cashDifference) },
    { label: '---', value: '---' },
    { label: 'Net cash', value: formatMoney(s.netCashInHand) },
    { label: 'Net card', value: formatMoney(s.netCardInHand) },
    { label: 'Net UPI', value: formatMoney(s.netUpiInHand) },
    { label: 'Expected tender total', value: formatMoney(s.actualHandInTotal) },
    { label: 'Bill count', value: String(s.billCount) },
    { label: 'Return count', value: String(s.returnCount) },
    { label: 'Gross card from bills', value: formatMoney(s.cardTotal) },
    { label: 'Gross UPI from bills', value: formatMoney(s.upiTotal) },
    { label: 'Credit note applied (bills)', value: formatMoney(s.creditNoteTotal) },
    { label: 'Return total amount', value: formatMoney(s.returnTotalAmount) },
    { label: 'Credit notes issued', value: formatMoney(s.creditNoteIssuedTotal) },
  ];
}

export function buildDetailSections(data: StoreDayCloseReportData): TableSection[] {
  const sections: TableSection[] = [
    {
      name: 'COUNTER_ROLLUP',
      headers: ['Counter', 'Status', 'Opening', 'Expected', 'Actual', 'Diff', 'Closed by', 'Closed at (local)'],
      rows: data.counterRollup.map((r) => [
        `POS${r.posCounter}`,
        r.status,
        formatMoney(r.openingCash),
        formatMoney(r.expectedCash),
        formatMoney(r.actualCashCounted),
        r.status === 'closed' ? formatMoney(r.cashDifference) : '—',
        r.closedBy ?? '—',
        r.closedAtLocal ?? '—',
      ]),
    },
    {
      name: 'BILLS',
      headers: [
        'Bill no', 'Posted (local)', 'Bill date', 'Counter', 'Customer', 'Mobile',
        'Qty', 'Payable', 'Cash', 'Card', 'UPI', 'Credit note', 'Credit note no(s)',
        'Returned', 'Return no', 'Adjustment', 'Adjustment no', 'Sync',
      ],
      rows: data.bills.map((b) => [
        b.billNo ?? '', b.postedAtLocal ?? '', b.billDate ?? '', b.counter ?? '', b.customer ?? '', b.mobile ?? '',
        b.qty ?? '', b.payable ?? '', b.cash ?? '', b.card ?? '', b.upi ?? '', b.creditNote ?? '', b.creditNoteRefs ?? '',
        b.returned ?? '', b.returnNo ?? '', b.adjustment ?? '', b.adjustmentNo ?? '', b.sync ?? '',
      ]),
    },
    {
      name: 'RETURNS',
      headers: [
        'Return no', 'Original bill', 'Counter', 'Posted (local)', 'Return total', 'Mode',
        'Cash refunded', 'Credit balance', 'Collected', 'Payments', 'Credit note no',
      ],
      rows: data.returns.map((r) => [
        r.returnNo ?? '', r.originalBill ?? '', r.counter ?? '', r.postedAtLocal ?? '', r.returnTotal ?? '', r.mode ?? '',
        r.cashRefunded ?? '', r.creditBalance ?? '', r.collected ?? '', r.payments ?? '', r.creditNoteNo ?? '',
      ]),
    },
    {
      name: 'ADJUSTMENTS',
      headers: [
        'Adjustment no', 'Original bill', 'Counter', 'Posted (local)',
        'Original payable', 'Adjusted payable', 'Diff payable', 'Reason',
      ],
      rows: data.adjustments.map((a) => [
        cell(a.adjustmentNo), cell(a.originalBill), cell(a.counter), cell(a.postedAtLocal),
        cell(a.originalPayable), cell(a.adjustedPayable), cell(a.diffPayable), cell(a.reason),
      ]),
    },
    {
      name: 'EXPENSES',
      headers: ['Expense no', 'Counter', 'Business date', 'Description', 'Amount'],
      rows: data.expenses.map((e) => [cell(e.expenseNo), cell(e.counter), cell(e.businessDate), cell(e.description), cell(e.amount)]),
    },
    {
      name: 'CASH_MOVEMENTS',
      headers: ['Movement no', 'Type', 'Counter', 'Amount', 'Note', 'Posted (local)'],
      rows: data.cashMovements.map((m) => [cell(m.movementNo), cell(m.type), cell(m.counter), cell(m.amount), cell(m.note), cell(m.postedAtLocal)]),
    },
  ];

  if (data.creditNoteCashouts.length > 0) {
    sections.push({
      name: 'CREDIT_NOTE_CASHOUTS',
      headers: ['Cashout no', 'Credit note no', 'Amount', 'Counter', 'Posted (local)'],
      rows: data.creditNoteCashouts.map((c) => [cell(c.cashoutNo), cell(c.creditNoteNo), cell(c.amount), cell(c.counter), cell(c.postedAtLocal)]),
    });
  }
  if (data.denominations.length > 0) {
    sections.push({
      name: 'DENOMINATIONS',
      headers: ['Counter', 'Denomination', 'Count', 'Subtotal'],
      rows: data.denominations.map((d) => [cell(d.counter), cell(d.denomination), cell(d.count), cell(d.subtotal)]),
    });
  }

  return sections;
}

export function buildCsvContent(data: StoreDayCloseReportData): string {
  const lines: string[] = [];
  const appendKv = (name: string, rows: KeyValueRow[]) => {
    lines.push(`--- ${name} ---`);
    for (const row of rows) {
      lines.push(`${escapeCsv(row.label)},${escapeCsv(row.value)}`);
    }
    lines.push('');
  };
  const appendTable = (section: TableSection) => {
    lines.push(`--- ${section.name} ---`);
    lines.push(section.headers.map(escapeCsv).join(','));
    for (const row of section.rows) {
      lines.push(row.map(escapeCsv).join(','));
    }
    lines.push('');
  };

  appendKv('METADATA', buildMetadataRows(data));
  appendKv('SUMMARY', buildSummaryRows(data));
  for (const section of buildDetailSections(data)) {
    appendTable(section);
  }
  return `\uFEFF${lines.join('\n')}`;
}
