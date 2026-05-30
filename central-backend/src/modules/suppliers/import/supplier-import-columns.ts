import type { ParsedSupplierImportRow } from './supplier-import.types';

export const SUPPLIER_IMPORT_SHEET_NAME = 'Suppliers';

export const SUPPLIER_IMPORT_HEADERS: readonly string[] = [
  'name',
  'gstNumber',
  'gstStateCode',
  'gstRegistrationType',
  'panNumber',
  'businessRelatedType',
  'contactPerson',
  'contactDescription',
  'mobileNo',
  'emailId',
  'faxNo',
  'offPhoneNo',
  'buildingAddress',
  'streetAddress',
  'landmark',
  'country',
  'state',
  'city',
  'pin',
  'isActive',
  'isSupplier',
] as const;

export const SUPPLIER_IMPORT_EXAMPLE_ROW: Record<string, string | boolean> = {
  name: 'Sharma Textiles',
  gstNumber: '27AABCU9603R1ZM',
  gstStateCode: '27',
  gstRegistrationType: 'Regular',
  panNumber: 'AABCU9603R',
  businessRelatedType: 'Wholesale',
  contactPerson: 'Raj Sharma',
  contactDescription: 'Primary vendor',
  mobileNo: '9876543210',
  emailId: 'contact@sharmatextiles.example',
  faxNo: '',
  offPhoneNo: '02212345678',
  buildingAddress: 'Unit 12',
  streetAddress: 'MG Road',
  landmark: 'Near City Mall',
  country: 'India',
  state: 'Maharashtra',
  city: 'Mumbai',
  pin: '400001',
  isActive: true,
  isSupplier: true,
};

const HEADER_ALIASES: Record<string, keyof ParsedSupplierImportRow> = {
  name: 'name',
  'supplier name': 'name',
  suppliername: 'name',
  supplier: 'name',
  gstnumber: 'gstNumber',
  'gst number': 'gstNumber',
  gst: 'gstNumber',
  gststatecode: 'gstStateCode',
  'gst state code': 'gstStateCode',
  gstregistrationtype: 'gstRegistrationType',
  'gst registration type': 'gstRegistrationType',
  pannumber: 'panNumber',
  'pan number': 'panNumber',
  pan: 'panNumber',
  businessrelatedtype: 'businessRelatedType',
  'business related type': 'businessRelatedType',
  contactperson: 'contactPerson',
  'contact person': 'contactPerson',
  contactdescription: 'contactDescription',
  'contact description': 'contactDescription',
  mobileno: 'mobileNo',
  'mobile no': 'mobileNo',
  mobile: 'mobileNo',
  phone: 'mobileNo',
  emailid: 'emailId',
  email: 'emailId',
  faxno: 'faxNo',
  'fax no': 'faxNo',
  offphoneno: 'offPhoneNo',
  'off phone no': 'offPhoneNo',
  buildingaddress: 'buildingAddress',
  'building address': 'buildingAddress',
  streetaddress: 'streetAddress',
  'street address': 'streetAddress',
  landmark: 'landmark',
  country: 'country',
  state: 'state',
  city: 'city',
  pin: 'pin',
  pincode: 'pin',
  'pin code': 'pin',
  isactive: 'isActive',
  issupplier: 'isSupplier',
};

const BOOLEAN_FIELDS = new Set<keyof ParsedSupplierImportRow>(['isActive', 'isSupplier']);

function normalizeHeader(h: string): string {
  return h.trim().toLowerCase().replace(/[_-]+/g, ' ');
}

export function mapHeaderToField(header: string): keyof ParsedSupplierImportRow | undefined {
  const key = normalizeHeader(header);
  return HEADER_ALIASES[key];
}

function parseCellValue(field: keyof ParsedSupplierImportRow, raw: unknown): unknown {
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
): ParsedSupplierImportRow[] {
  const fieldIndexes: Array<{ field: keyof ParsedSupplierImportRow; index: number }> = [];
  headers.forEach((h, i) => {
    const field = mapHeaderToField(h);
    if (field && field !== 'rowNumber') fieldIndexes.push({ field, index: i });
  });

  const parsed: ParsedSupplierImportRow[] = [];
  let rowNumber = startRowNumber;
  for (const row of dataRows) {
    if (!row || row.every((c) => c === undefined || c === null || String(c).trim() === '')) {
      rowNumber++;
      continue;
    }
    const item: ParsedSupplierImportRow = { rowNumber };
    for (const { field, index } of fieldIndexes) {
      const val = parseCellValue(field, row[index]);
      if (val !== undefined) (item as unknown as Record<string, unknown>)[field] = val;
    }
    parsed.push(item);
    rowNumber++;
  }
  return parsed;
}
