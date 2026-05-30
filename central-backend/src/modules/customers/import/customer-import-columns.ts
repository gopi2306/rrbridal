import type { ParsedCustomerImportRow } from './customer-import.types';

export const CUSTOMER_IMPORT_SHEET_NAME = 'Customers';

export const CUSTOMER_IMPORT_HEADERS: readonly string[] = [
  'customerCode',
  'name',
  'phone',
  'email',
  'gstin',
  'addressLine1',
  'addressLine2',
  'city',
  'state',
  'pincode',
  'isActive',
] as const;

export const CUSTOMER_IMPORT_EXAMPLE_ROW: Record<string, string | boolean> = {
  customerCode: 'CUST-001',
  name: 'Priya Sharma',
  phone: '9876543210',
  email: 'priya.sharma@example.com',
  gstin: '',
  addressLine1: '12 Jubilee Hills',
  addressLine2: 'Near Peddamma Temple',
  city: 'Hyderabad',
  state: 'Telangana',
  pincode: '500033',
  isActive: true,
};

const HEADER_ALIASES: Record<string, keyof ParsedCustomerImportRow> = {
  customercode: 'customerCode',
  'customer code': 'customerCode',
  code: 'customerCode',
  name: 'name',
  'customer name': 'name',
  customername: 'name',
  phone: 'phone',
  mobile: 'phone',
  mobileno: 'phone',
  'mobile no': 'phone',
  email: 'email',
  emailid: 'email',
  'email id': 'email',
  gstin: 'gstin',
  gst: 'gstin',
  addressline1: 'addressLine1',
  'address line 1': 'addressLine1',
  address1: 'addressLine1',
  addressline2: 'addressLine2',
  'address line 2': 'addressLine2',
  address2: 'addressLine2',
  city: 'city',
  state: 'state',
  pincode: 'pincode',
  pin: 'pincode',
  'pin code': 'pincode',
  isactive: 'isActive',
};

const BOOLEAN_FIELDS = new Set<keyof ParsedCustomerImportRow>(['isActive']);

function normalizeHeader(h: string): string {
  return h.trim().toLowerCase().replace(/[_-]+/g, ' ');
}

export function mapHeaderToField(header: string): keyof ParsedCustomerImportRow | undefined {
  const key = normalizeHeader(header);
  return HEADER_ALIASES[key];
}

function parseCellValue(field: keyof ParsedCustomerImportRow, raw: unknown): unknown {
  if (raw === undefined || raw === null || raw === '') return undefined;
  if (BOOLEAN_FIELDS.has(field)) {
    const s = String(raw).trim().toLowerCase();
    if (['true', '1', 'yes', 'y'].includes(s)) return true;
    if (['false', '0', 'no', 'n'].includes(s)) return false;
    return undefined;
  }
  return String(raw).trim() || undefined;
}

export function rowArraysToParsedRows(
  headers: string[],
  dataRows: unknown[][],
  startRowNumber = 2,
): ParsedCustomerImportRow[] {
  const fieldIndexes: Array<{ field: keyof ParsedCustomerImportRow; index: number }> = [];
  headers.forEach((h, i) => {
    const field = mapHeaderToField(h);
    if (field && field !== 'rowNumber') fieldIndexes.push({ field, index: i });
  });

  const parsed: ParsedCustomerImportRow[] = [];
  let rowNumber = startRowNumber;
  for (const row of dataRows) {
    if (!row || row.every((c) => c === undefined || c === null || String(c).trim() === '')) {
      rowNumber++;
      continue;
    }
    const item: ParsedCustomerImportRow = { rowNumber };
    for (const { field, index } of fieldIndexes) {
      const val = parseCellValue(field, row[index]);
      if (val !== undefined) (item as unknown as Record<string, unknown>)[field] = val;
    }
    parsed.push(item);
    rowNumber++;
  }
  return parsed;
}
