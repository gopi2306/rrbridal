import { BadRequestException, Injectable } from '@nestjs/common';
import * as XLSX from 'xlsx';
import { CreateCustomerDto } from '../dto/create-customer.dto';
import { CustomersService } from '../customers.service';
import {
  CUSTOMER_IMPORT_EXAMPLE_ROW,
  CUSTOMER_IMPORT_HEADERS,
  CUSTOMER_IMPORT_SHEET_NAME,
  rowArraysToParsedRows,
} from './customer-import-columns';
import type {
  CustomerImportOptions,
  CustomerImportResult,
  ParsedCustomerImportRow,
} from './customer-import.types';

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

@Injectable()
export class CustomerImportService {
  constructor(private readonly customersService: CustomersService) {}

  buildExcelTemplate(): Buffer {
    const headerRow = [...CUSTOMER_IMPORT_HEADERS];
    const exampleRow = CUSTOMER_IMPORT_HEADERS.map((h) => {
      const v = CUSTOMER_IMPORT_EXAMPLE_ROW[h];
      return v === undefined || v === '' ? '' : v;
    });
    const sheet = XLSX.utils.aoa_to_sheet([headerRow, exampleRow]);
    const workbook = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(workbook, sheet, CUSTOMER_IMPORT_SHEET_NAME);
    return Buffer.from(XLSX.write(workbook, { type: 'buffer', bookType: 'xlsx' }));
  }

  async runFromBuffer(
    buffer: Buffer,
    originalName: string,
    options: CustomerImportOptions = {},
  ): Promise<CustomerImportResult> {
    const lower = originalName.toLowerCase();
    const isCsv = lower.endsWith('.csv');
    const isXlsx = lower.endsWith('.xlsx') || lower.endsWith('.xls');
    if (!isCsv && !isXlsx) {
      throw new BadRequestException('File must be .csv or .xlsx');
    }

    const workbook = XLSX.read(buffer, { type: 'buffer', raw: false });
    const sheet =
      workbook.Sheets[CUSTOMER_IMPORT_SHEET_NAME] ??
      workbook.Sheets[workbook.SheetNames[0] ?? ''];
    if (!sheet) throw new BadRequestException('Workbook has no sheets');

    const rows = XLSX.utils.sheet_to_json<unknown[]>(sheet, { header: 1, defval: '' }) as unknown[][];
    if (rows.length < 2) {
      throw new BadRequestException('File must have a header row and at least one data row');
    }

    const headers = (rows[0] ?? []).map((c) => String(c ?? ''));
    const parsed = rowArraysToParsedRows(headers, rows.slice(1), 2);
    return await this.runParsedRows(parsed, options);
  }

  async runFromExcelBuffer(
    buffer: Buffer,
    options: CustomerImportOptions = {},
  ): Promise<CustomerImportResult> {
    const workbook = XLSX.read(buffer, { type: 'buffer', raw: false });
    const sheet =
      workbook.Sheets[CUSTOMER_IMPORT_SHEET_NAME] ??
      workbook.Sheets[workbook.SheetNames[0] ?? ''];
    if (!sheet) throw new BadRequestException('Excel file has no sheets');

    const rows = XLSX.utils.sheet_to_json<unknown[]>(sheet, { header: 1, defval: '' }) as unknown[][];
    if (rows.length < 2) {
      throw new BadRequestException('Excel must have a header row and at least one data row');
    }

    const headers = (rows[0] ?? []).map((c) => String(c ?? ''));
    const parsed = rowArraysToParsedRows(headers, rows.slice(1), 2);
    return await this.runParsedRows(parsed, options);
  }

  private async runParsedRows(
    rows: ParsedCustomerImportRow[],
    options: CustomerImportOptions,
  ): Promise<CustomerImportResult> {
    const dryRun = options.dryRun === true;

    const result: CustomerImportResult = {
      totalRows: rows.length,
      created: 0,
      updated: 0,
      failed: 0,
      errors: [],
      dryRun,
    };

    for (const row of rows) {
      const nameLabel = row.name?.trim();
      const codeLabel = row.customerCode?.trim();
      if (!nameLabel) {
        result.failed++;
        result.errors.push({ row: row.rowNumber, message: 'name is required' });
        continue;
      }

      try {
        const dto = this.rowToCreateDto(row);
        if (dryRun) {
          const existing = await this.customersService.findExistingForImport(dto);
          if (existing) result.updated++;
          else result.created++;
          continue;
        }

        const outcome = await this.customersService.upsertForImport(dto);
        if (outcome.created) result.created++;
        else result.updated++;
      } catch (err: unknown) {
        result.failed++;
        const errEntry: { row: number; customerCode?: string; message: string } = {
          row: row.rowNumber,
          message: err instanceof Error ? err.message : String(err),
        };
        if (codeLabel) errEntry.customerCode = codeLabel;
        result.errors.push(errEntry);
      }
    }

    return result;
  }

  private rowToCreateDto(row: ParsedCustomerImportRow): CreateCustomerDto {
    const name = row.name?.trim();
    if (!name) {
      throw new Error('name is required');
    }

    if (row.email?.trim() && !EMAIL_RE.test(row.email.trim())) {
      throw new Error(`Invalid email: ${row.email}`);
    }

    const dto: CreateCustomerDto = { name };

    if (row.customerCode) dto.customerCode = row.customerCode.trim();
    if (row.phone) dto.phone = row.phone;
    if (row.email) dto.email = row.email.trim();
    if (row.gstin) dto.gstin = row.gstin;
    if (row.addressLine1) dto.addressLine1 = row.addressLine1;
    if (row.addressLine2) dto.addressLine2 = row.addressLine2;
    if (row.city) dto.city = row.city;
    if (row.state) dto.state = row.state;
    if (row.pincode) dto.pincode = row.pincode;
    if (row.isActive !== undefined) dto.isActive = row.isActive;

    return dto;
  }
}
