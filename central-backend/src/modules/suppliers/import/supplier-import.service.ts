import { BadRequestException, Injectable } from '@nestjs/common';
import * as XLSX from 'xlsx';
import { CreateSupplierDto } from '../dto/create-supplier.dto';
import { SuppliersService } from '../suppliers.service';
import {
  SUPPLIER_IMPORT_EXAMPLE_ROW,
  SUPPLIER_IMPORT_HEADERS,
  SUPPLIER_IMPORT_SHEET_NAME,
  rowArraysToParsedRows,
} from './supplier-import-columns';
import type {
  ParsedSupplierImportRow,
  SupplierImportOptions,
  SupplierImportResult,
} from './supplier-import.types';

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

@Injectable()
export class SupplierImportService {
  constructor(private readonly suppliersService: SuppliersService) {}

  buildExcelTemplate(): Buffer {
    const headerRow = [...SUPPLIER_IMPORT_HEADERS];
    const exampleRow = SUPPLIER_IMPORT_HEADERS.map((h) => {
      const v = SUPPLIER_IMPORT_EXAMPLE_ROW[h];
      return v === undefined || v === '' ? '' : v;
    });
    const sheet = XLSX.utils.aoa_to_sheet([headerRow, exampleRow]);
    const workbook = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(workbook, sheet, SUPPLIER_IMPORT_SHEET_NAME);
    return Buffer.from(XLSX.write(workbook, { type: 'buffer', bookType: 'xlsx' }));
  }

  async runFromBuffer(
    buffer: Buffer,
    originalName: string,
    options: SupplierImportOptions = {},
  ): Promise<SupplierImportResult> {
    const lower = originalName.toLowerCase();
    const isCsv = lower.endsWith('.csv');
    const isXlsx = lower.endsWith('.xlsx') || lower.endsWith('.xls');
    if (!isCsv && !isXlsx) {
      throw new BadRequestException('File must be .csv or .xlsx');
    }

    const workbook = XLSX.read(buffer, { type: 'buffer', raw: false });
    const sheet =
      workbook.Sheets[SUPPLIER_IMPORT_SHEET_NAME] ??
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
    options: SupplierImportOptions = {},
  ): Promise<SupplierImportResult> {
    const workbook = XLSX.read(buffer, { type: 'buffer', raw: false });
    const sheet =
      workbook.Sheets[SUPPLIER_IMPORT_SHEET_NAME] ??
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
    rows: ParsedSupplierImportRow[],
    options: SupplierImportOptions,
  ): Promise<SupplierImportResult> {
    const dryRun = options.dryRun === true;

    const result: SupplierImportResult = {
      totalRows: rows.length,
      created: 0,
      updated: 0,
      failed: 0,
      errors: [],
      dryRun,
    };

    for (const row of rows) {
      const nameLabel = row.name?.trim();
      if (!nameLabel) {
        result.failed++;
        result.errors.push({ row: row.rowNumber, message: 'name is required' });
        continue;
      }

      try {
        const dto = this.rowToCreateDto(row);
        if (dryRun) {
          const existing = await this.suppliersService.findByName(nameLabel);
          if (existing) result.updated++;
          else result.created++;
          continue;
        }

        const outcome = await this.suppliersService.upsertByName(nameLabel, dto);
        if (outcome.created) result.created++;
        else result.updated++;
      } catch (err: unknown) {
        result.failed++;
        const errEntry: { row: number; name?: string; message: string } = {
          row: row.rowNumber,
          message: err instanceof Error ? err.message : String(err),
        };
        if (nameLabel) errEntry.name = nameLabel;
        result.errors.push(errEntry);
      }
    }

    return result;
  }

  private rowToCreateDto(row: ParsedSupplierImportRow): CreateSupplierDto {
    const name = row.name?.trim();
    if (!name) {
      throw new Error('name is required');
    }

    if (row.emailId?.trim() && !EMAIL_RE.test(row.emailId.trim())) {
      throw new Error(`Invalid emailId: ${row.emailId}`);
    }

    const dto: CreateSupplierDto = { name };

    if (row.gstNumber) dto.gstNumber = row.gstNumber;
    if (row.gstStateCode) dto.gstStateCode = row.gstStateCode;
    if (row.gstRegistrationType) dto.gstRegistrationType = row.gstRegistrationType;
    if (row.panNumber) dto.panNumber = row.panNumber;
    if (row.businessRelatedType) dto.businessRelatedType = row.businessRelatedType;
    if (row.contactPerson) dto.contactPerson = row.contactPerson;
    if (row.contactDescription) dto.contactDescription = row.contactDescription;
    if (row.mobileNo) dto.mobileNo = row.mobileNo;
    if (row.emailId) dto.emailId = row.emailId.trim();
    if (row.faxNo) dto.faxNo = row.faxNo;
    if (row.offPhoneNo) dto.offPhoneNo = row.offPhoneNo;
    if (row.buildingAddress) dto.buildingAddress = row.buildingAddress;
    if (row.streetAddress) dto.streetAddress = row.streetAddress;
    if (row.landmark) dto.landmark = row.landmark;
    if (row.country) dto.country = row.country;
    if (row.state) dto.state = row.state;
    if (row.city) dto.city = row.city;
    if (row.pin) dto.pin = row.pin;
    if (row.isActive !== undefined) dto.isActive = row.isActive;
    if (row.isSupplier !== undefined) dto.isSupplier = row.isSupplier;

    return dto;
  }
}
