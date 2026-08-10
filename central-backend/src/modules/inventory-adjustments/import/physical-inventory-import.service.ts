import { BadRequestException, Injectable } from '@nestjs/common';
import * as XLSX from 'xlsx';
import { buildExcelBuffer } from '../../../common/tabular-export';
import { ProductsService } from '../../products/products.service';
import {
  InventoryAdjustmentsService,
  PhysicalInventorySetToLine,
} from '../inventory-adjustments.service';
import {
  PHYSICAL_INVENTORY_HEADERS,
  PHYSICAL_INVENTORY_MAX_ROWS,
  PHYSICAL_INVENTORY_SHEET,
  parsePhysicalInventoryRows,
} from './physical-inventory-import-columns';
import {
  PhysicalInventoryImportPreviewLine,
  PhysicalInventoryImportResult,
} from './physical-inventory-import.types';

@Injectable()
export class PhysicalInventoryImportService {
  constructor(
    private readonly productsService: ProductsService,
    private readonly adjustmentsService: InventoryAdjustmentsService,
  ) {}

  async buildTemplate(storeCode: string): Promise<Buffer> {
    await this.adjustmentsService.validateStoreCode(storeCode);
    const products = await this.productsService.listPhysicalInventoryTemplateProducts();
    const rows = products.map((product) => [
      typeof product.sku === 'string' ? product.sku : '',
      typeof product.itemName === 'string' ? product.itemName : '',
      '',
    ]);
    return buildExcelBuffer(PHYSICAL_INVENTORY_HEADERS, rows, PHYSICAL_INVENTORY_SHEET);
  }

  async runFromExcelBuffer(input: {
    buffer: Buffer;
    originalName: string;
    storeCode: string;
    reason: string;
    dryRun: boolean;
    batchId?: string;
  }): Promise<PhysicalInventoryImportResult> {
    this.validateRequest(input);
    const workbook = XLSX.read(input.buffer, { type: 'buffer', raw: false });
    const sheet =
      workbook.Sheets[PHYSICAL_INVENTORY_SHEET] ??
      workbook.Sheets[workbook.SheetNames[0] ?? ''];
    if (!sheet) throw new BadRequestException('Excel workbook has no sheets');
    const matrix = XLSX.utils.sheet_to_json<unknown[]>(sheet, {
      header: 1,
      defval: '',
      raw: false,
    }) as unknown[][];
    if (matrix.length < 2) {
      throw new BadRequestException('Excel must have a header row and at least one data row');
    }
    if (matrix.length - 1 > PHYSICAL_INVENTORY_MAX_ROWS) {
      throw new BadRequestException(
        `Excel exceeds the ${PHYSICAL_INVENTORY_MAX_ROWS} row limit`,
      );
    }

    let parsed: ReturnType<typeof parsePhysicalInventoryRows>;
    try {
      parsed = parsePhysicalInventoryRows(matrix[0] ?? [], matrix.slice(1), 2);
    } catch (err) {
      throw new BadRequestException(err instanceof Error ? err.message : String(err));
    }

    const errors = [...parsed.errors];
    const products = await this.productsService.findPhysicalInventoryProductsBySkus(
      parsed.rows.map((row) => row.sku),
    );
    const productBySku = new Map(
      products.map((product) => [
        typeof product.sku === 'string' ? product.sku : '',
        typeof product.itemName === 'string' ? product.itemName : '',
      ]),
    );
    const validRows = parsed.rows.filter((row) => {
      if (productBySku.has(row.sku)) return true;
      errors.push({ row: row.row, sku: row.sku, message: 'SKU was not found in active products' });
      return false;
    });

    const previewLines: PhysicalInventoryImportPreviewLine[] = [];
    if (validRows.length > 0) {
      const preview = await this.adjustmentsService.previewStorePhysicalCount(
        input.storeCode,
        validRows.map((row) => ({ sku: row.sku, newQty: row.newQty })),
      );
      const sourceBySku = new Map(validRows.map((row) => [row.sku, row]));
      for (const line of preview.lines) {
        const source = sourceBySku.get(line.sku)!;
        const centralName = productBySku.get(line.sku) ?? line.itemName;
        const previewLine: PhysicalInventoryImportPreviewLine = {
          row: source.row,
          sku: line.sku,
          itemName: centralName,
          qtyBefore: line.qtyBefore,
          newQty: line.newQty,
          qtyDelta: line.qtyDelta,
          qtyAfter: line.qtyAfter,
          unchanged: line.unchanged,
        };
        if (
          source.itemName &&
          centralName &&
          source.itemName.localeCompare(centralName, undefined, { sensitivity: 'accent' }) !== 0
        ) {
          previewLine.warning = `Item Name differs from central: ${centralName}`;
        }
        previewLines.push(previewLine);
      }
    }

    const changed = previewLines.filter((line) => !line.unchanged);
    const result: PhysicalInventoryImportResult = {
      dryRun: input.dryRun,
      readyToCommit: errors.length === 0 && changed.length > 0,
      totalRows: parsed.sourceRows,
      adjusted: changed.length,
      skipped: previewLines.length - changed.length,
      failed: errors.length,
      errors,
      lines: previewLines,
    };
    if (input.dryRun || !result.readyToCommit) return result;

    const committed = await this.adjustmentsService.createStorePhysicalImport({
      storeCode: input.storeCode,
      reason: input.reason,
      batchId: input.batchId!,
      lines: validRows.map(
        (row): PhysicalInventorySetToLine => ({ sku: row.sku, newQty: row.newQty }),
      ),
    });
    if (committed) {
      if (typeof committed.id === 'string') result.adjustmentId = committed.id;
      if (typeof committed.adjustmentNo === 'string') {
        result.adjustmentNo = committed.adjustmentNo;
      }
    }
    return result;
  }

  private validateRequest(input: {
    originalName: string;
    storeCode: string;
    reason: string;
    dryRun: boolean;
    batchId?: string;
  }) {
    const lower = input.originalName.toLowerCase();
    if (!lower.endsWith('.xlsx') && !lower.endsWith('.xls')) {
      throw new BadRequestException('Only .xlsx or .xls files are allowed');
    }
    if (!input.storeCode?.trim()) throw new BadRequestException('storeCode is required');
    if (!input.reason?.trim()) throw new BadRequestException('reason is required');
    if (input.reason.trim().length > 500) {
      throw new BadRequestException('reason must not exceed 500 characters');
    }
    if (!input.dryRun && !input.batchId?.trim()) {
      throw new BadRequestException('batchId is required when committing an import');
    }
  }
}
