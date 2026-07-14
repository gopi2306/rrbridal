import { BadRequestException, Injectable } from '@nestjs/common';
import * as XLSX from 'xlsx';
import { AuditActor } from '../../audit-logs/audit-change.util';
import { AuditLogsService } from '../../audit-logs/audit-logs.service';
import { CreateProductDto } from '../dto/create-product.dto';
import { ProductsService } from '../products.service';
import { MasterLookupService } from './master-lookup.service';
import {
  PRODUCT_IMPORT_EXAMPLE_ROW,
  PRODUCT_IMPORT_HEADERS,
  PRODUCT_IMPORT_SHEET_NAME,
  rowArraysToParsedRows,
} from './product-import-columns';
import type { ParsedProductImportRow, ProductImportOptions, ProductImportResult } from './product-import.types';

@Injectable()
export class ProductImportService {
  constructor(
    private readonly productsService: ProductsService,
    private readonly masterLookup: MasterLookupService,
    private readonly auditLogs: AuditLogsService,
  ) {}

  buildExcelTemplate(): Buffer {
    const headerRow = [...PRODUCT_IMPORT_HEADERS];
    const exampleRow = PRODUCT_IMPORT_HEADERS.map((h) => {
      const v = PRODUCT_IMPORT_EXAMPLE_ROW[h];
      return v === undefined || v === '' ? '' : v;
    });
    const sheet = XLSX.utils.aoa_to_sheet([headerRow, exampleRow]);
    const workbook = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(workbook, sheet, PRODUCT_IMPORT_SHEET_NAME);
    return Buffer.from(XLSX.write(workbook, { type: 'buffer', bookType: 'xlsx' }));
  }

  async runFromBuffer(
    buffer: Buffer,
    originalName: string,
    options: ProductImportOptions = {},
    actor?: AuditActor,
  ): Promise<ProductImportResult> {
    const lower = originalName.toLowerCase();
    const isCsv = lower.endsWith('.csv');
    const isXlsx = lower.endsWith('.xlsx') || lower.endsWith('.xls');
    if (!isCsv && !isXlsx) {
      throw new BadRequestException('File must be .csv or .xlsx');
    }

    const workbook = XLSX.read(buffer, { type: 'buffer', raw: false });
    const sheet =
      workbook.Sheets[PRODUCT_IMPORT_SHEET_NAME] ??
      workbook.Sheets[workbook.SheetNames[0] ?? ''];
    if (!sheet) throw new BadRequestException('Workbook has no sheets');

    const rows = XLSX.utils.sheet_to_json<unknown[]>(sheet, { header: 1, defval: '' }) as unknown[][];
    if (rows.length < 2) {
      throw new BadRequestException('File must have a header row and at least one data row');
    }

    const headers = (rows[0] ?? []).map((c) => String(c ?? ''));
    const dataRows = rows.slice(1);
    const parsed = rowArraysToParsedRows(headers, dataRows, 2);
    return await this.runParsedRows(parsed, options, actor, originalName);
  }

  async runFromExcelBuffer(
    buffer: Buffer,
    options: ProductImportOptions = {},
    actor?: AuditActor,
  ): Promise<ProductImportResult> {
    const workbook = XLSX.read(buffer, { type: 'buffer', raw: false });
    const sheet =
      workbook.Sheets[PRODUCT_IMPORT_SHEET_NAME] ??
      workbook.Sheets[workbook.SheetNames[0] ?? ''];
    if (!sheet) throw new BadRequestException('Excel file has no sheets');

    const rows = XLSX.utils.sheet_to_json<unknown[]>(sheet, { header: 1, defval: '' }) as unknown[][];
    if (rows.length < 2) {
      throw new BadRequestException('Excel must have a header row and at least one data row');
    }

    const headers = (rows[0] ?? []).map((c) => String(c ?? ''));
    const parsed = rowArraysToParsedRows(headers, rows.slice(1), 2);
    return await this.runParsedRows(parsed, options, actor, 'import.xlsx');
  }

  private async runParsedRows(
    rows: ParsedProductImportRow[],
    options: ProductImportOptions,
    actor?: AuditActor,
    sourceFile = 'import',
  ): Promise<ProductImportResult> {
    const dryRun = options.dryRun === true;
    const createMissing = options.createMissingMasters !== false;

    this.masterLookup.beginRun();

    const result: ProductImportResult = {
      totalRows: rows.length,
      created: 0,
      updated: 0,
      failed: 0,
      mastersCreated: {},
      errors: [],
      dryRun,
    };

    for (const row of rows) {
      try {
        const dto = await this.rowToCreateDto(row, createMissing);
        if (dryRun) {
          const existing = await this.productsService.findExistingForImport(row.sku, row.itemName);
          if (existing) result.updated++;
          else result.created++;
          continue;
        }

        const outcome = await this.productsService.upsertBySku(dto, actor);
        if (outcome.created) result.created++;
        else result.updated++;
      } catch (err: unknown) {
        result.failed++;
        const errEntry: { row: number; sku?: string; message: string } = {
          row: row.rowNumber,
          message: err instanceof Error ? err.message : String(err),
        };
        if (row.sku) errEntry.sku = row.sku;
        result.errors.push(errEntry);
      }
    }

    result.mastersCreated = this.masterLookup.getMastersCreated();

    if (!dryRun && (result.created > 0 || result.updated > 0 || result.failed > 0)) {
      const importAudit: {
        entityType: string;
        entityId: string;
        action: string;
        metadata: Record<string, unknown>;
        actor?: AuditActor;
      } = {
        entityType: 'product',
        entityId: 'import-batch',
        action: 'imported',
        metadata: {
          sourceFile,
          totalRows: result.totalRows,
          created: result.created,
          updated: result.updated,
          failed: result.failed,
          mastersCreated: result.mastersCreated,
        },
      };
      if (actor) importAudit.actor = actor;
      await this.auditLogs.logEvent(importAudit);
    }

    return result;
  }

  private async rowToCreateDto(
    row: ParsedProductImportRow,
    createMissing: boolean,
  ): Promise<CreateProductDto> {
    if (!row.itemName?.trim()) {
      throw new Error('itemName is required');
    }
    if (!row.supplierName?.trim()) {
      throw new Error('supplierName is required');
    }
    if (!row.departmentName?.trim()) {
      throw new Error('departmentName is required');
    }
    if (!row.categoryName?.trim()) {
      throw new Error('categoryName is required');
    }

    const refs = await this.masterLookup.resolveRowToProductRefs(row, createMissing);

    if (!refs.supplierNameId) throw new Error('Could not resolve supplierName');
    if (!refs.departmentId) throw new Error('Could not resolve departmentName');
    if (!refs.categoryId) throw new Error('Could not resolve categoryName');

    const dto: CreateProductDto = {
      itemName: row.itemName.trim(),
      supplierNameId: refs.supplierNameId,
      departmentId: refs.departmentId,
      categoryId: refs.categoryId,
    };

    if (row.sku?.trim()) dto.sku = row.sku.trim();
    if (row.shortName) dto.shortName = row.shortName;
    if (row.alias) dto.alias = row.alias;
    if (refs.subCategoryId) dto.subCategoryId = refs.subCategoryId;
    if (refs.manufacturerNameId) dto.manufacturerNameId = refs.manufacturerNameId;
    if (refs.brandId) dto.brandId = refs.brandId;
    if (refs.colourId) dto.colourId = refs.colourId;
    if (refs.colourTypeId) dto.colourTypeId = refs.colourTypeId;
    if (refs.productStatusId) dto.productStatusId = refs.productStatusId;
    if (refs.hsnCodeId) dto.hsnCodeId = refs.hsnCodeId;
    if (refs.gstUomId) dto.gstUomId = refs.gstUomId;
    if (refs.uomSubId) dto.uomSubId = refs.uomSubId;
    if (refs.weightAndSizeId) dto.weightAndSizeId = refs.weightAndSizeId;
    if (refs.weightPerGmOrMlId) dto.weightPerGmOrMlId = refs.weightPerGmOrMlId;
    if (refs.offerGroupId) dto.offerGroupId = refs.offerGroupId;
    if (refs.skuTypeId) dto.skuTypeId = refs.skuTypeId;
    if (refs.skuOrderGroupId) dto.skuOrderGroupId = refs.skuOrderGroupId;
    if (refs.indentTypeId) dto.indentTypeId = refs.indentTypeId;
    if (refs.batchExpiryDetailId) dto.batchExpiryDetailId = refs.batchExpiryDetailId;
    if (refs.itemPrepStatusId) dto.itemPrepStatusId = refs.itemPrepStatusId;
    if (refs.packedConfirmationId) dto.packedConfirmationId = refs.packedConfirmationId;
    if (refs.poQtyPolicyId) dto.poQtyPolicyId = refs.poQtyPolicyId;
    if (refs.sellById) dto.sellById = refs.sellById;
    if (refs.batchSelectionId) dto.batchSelectionId = refs.batchSelectionId;
    if (row.itemProductType) dto.itemProductType = row.itemProductType;
    if (row.gstCode) dto.gstCode = row.gstCode;
    if (row.gstPercent !== undefined) dto.gstPercent = row.gstPercent;
    if (row.upcEanCode) dto.upcEanCode = row.upcEanCode;
    if (row.subUomConversion !== undefined) dto.subUomConversion = row.subUomConversion;
    if (row.grindingCharge !== undefined) dto.grindingCharge = row.grindingCharge;
    if (row.weightGms !== undefined) dto.weightGms = row.weightGms;
    if (row.decimalPoint !== undefined) dto.decimalPoint = row.decimalPoint;
    if (row.minimumShelfFit !== undefined) dto.minimumShelfFit = row.minimumShelfFit;
    if (row.itemPerUnit !== undefined) dto.itemPerUnit = row.itemPerUnit;
    if (row.costPrice !== undefined) dto.costPrice = row.costPrice;
    if (row.marginPercent !== undefined) dto.marginPercent = row.marginPercent;
    if (row.mrp !== undefined) dto.mrp = row.mrp;
    if (row.sellingPrice !== undefined) dto.sellingPrice = row.sellingPrice;
    if (row.storePrice !== undefined) dto.storePrice = row.storePrice;
    if (row.minStock !== undefined) dto.minStock = row.minStock;
    if (row.reorderLevel !== undefined) dto.reorderLevel = row.reorderLevel;
    if (row.unit) dto.unit = row.unit;
    if (row.isActive !== undefined) dto.isActive = row.isActive;
    if (row.itemDiscountAllowed !== undefined) dto.itemDiscountAllowed = row.itemDiscountAllowed;
    if (row.isWeighable !== undefined) dto.isWeighable = row.isWeighable;

    return dto;
  }
}
