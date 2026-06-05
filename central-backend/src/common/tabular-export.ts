import * as XLSX from 'xlsx';

export const TABULAR_EXPORT_MAX_ROWS = 10_000;

export function buildExcelBuffer(
  headers: readonly string[],
  rows: string[][],
  sheetName = 'Export',
): Buffer {
  const sheet = XLSX.utils.aoa_to_sheet([[...headers], ...rows]);
  const workbook = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(workbook, sheet, sheetName);
  return Buffer.from(XLSX.write(workbook, { type: 'buffer', bookType: 'xlsx' }));
}

export function buildExportFilename(prefix: string, scopeCode: string, format: string): string {
  const date = new Date().toISOString().slice(0, 10);
  const ext = format === 'xlsx' ? 'xlsx' : format;
  const safeCode = scopeCode.replace(/[^a-z0-9-]+/gi, '-').toLowerCase();
  return `${prefix}-${safeCode}-${date}.${ext}`;
}

export type TabularExportSheet = {
  name: string;
  headers: readonly string[];
  rows: string[][];
};

export function formatExportMoney(value: number): string {
  if (!Number.isFinite(value)) return '';
  return value.toFixed(2);
}

/** Margin as percentage of cost value: (margin / costValue) × 100 */
export function formatExportMarginPercent(margin: number, costValue: number): string {
  if (!Number.isFinite(margin) || !Number.isFinite(costValue) || costValue <= 0) return '';
  return ((margin / costValue) * 100).toFixed(2);
}

export function buildMultiSheetExcelBuffer(sheets: readonly TabularExportSheet[]): Buffer {
  const workbook = XLSX.utils.book_new();
  for (const sheet of sheets) {
    const ws = XLSX.utils.aoa_to_sheet([[...sheet.headers], ...sheet.rows]);
    XLSX.utils.book_append_sheet(workbook, ws, sheet.name.slice(0, 31));
  }
  return Buffer.from(XLSX.write(workbook, { type: 'buffer', bookType: 'xlsx' }));
}
