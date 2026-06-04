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
