import { Injectable } from '@nestjs/common';
import * as XLSX from 'xlsx';
import { StoresService } from '../stores/stores.service';
import type { InventoryExportFormat } from './dto/inventory-export-query.dto';
import { InventoryService } from './inventory.service';
import {
  INVENTORY_EXPORT_HEADERS,
  gridRowsToMatrix,
  matrixToCsv,
} from './export/inventory-export-columns';
import { buildInventoryPdfBuffer } from './export/inventory-export-pdf';

export type InventoryExportResult = {
  buffer: Buffer;
  contentType: string;
  filename: string;
};

const SHEET_NAME = 'Inventory';

@Injectable()
export class InventoryExportService {
  constructor(
    private readonly inventoryService: InventoryService,
    private readonly storesService: StoresService,
  ) {}

  async buildExport(params: {
    format: InventoryExportFormat;
    search?: string;
    storeId?: string;
  }): Promise<InventoryExportResult> {
    const storeContext = await this.resolveStoreContext(params.storeId);
    const rows = await this.inventoryService.fetchAllWarehouseStoreRows({
      ...(params.search !== undefined && params.search !== '' ? { search: params.search } : {}),
      ...(params.storeId !== undefined && params.storeId !== '' ? { storeId: params.storeId } : {}),
    });
    const matrix = gridRowsToMatrix(rows);
    const filename = this.buildFilename(params.format, storeContext.code);
    const context = this.buildContextLine(params.search, storeContext.label);

    switch (params.format) {
      case 'xlsx':
        return {
          buffer: this.buildExcelBuffer(matrix),
          contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
          filename,
        };
      case 'csv':
        return {
          buffer: matrixToCsv(INVENTORY_EXPORT_HEADERS, matrix),
          contentType: 'text/csv; charset=utf-8',
          filename,
        };
      case 'pdf':
        return {
          buffer: await buildInventoryPdfBuffer(matrix, context),
          contentType: 'application/pdf',
          filename,
        };
      default:
        return {
          buffer: this.buildExcelBuffer(matrix),
          contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
          filename,
        };
    }
  }

  private async resolveStoreContext(storeId?: string): Promise<{ code: string; label: string }> {
    if (!storeId?.trim()) {
      return { code: 'all-stores', label: 'All stores' };
    }
    try {
      const store = await this.storesService.findByCode(storeId.trim());
      return { code: store.code, label: `${store.name} (${store.code})` };
    } catch {
      const code = storeId.trim().toLowerCase();
      return { code, label: code };
    }
  }

  private buildFilename(format: InventoryExportFormat, storeCode: string): string {
    const date = new Date().toISOString().slice(0, 10);
    const ext = format === 'xlsx' ? 'xlsx' : format;
    return `inventory-${storeCode}-${date}.${ext}`;
  }

  private buildContextLine(search: string | undefined, storeLabel: string): string {
    const parts = [`Store: ${storeLabel}`];
    if (search?.trim()) parts.push(`Search: ${search.trim()}`);
    parts.push(`Generated: ${new Date().toISOString()}`);
    return parts.join(' · ');
  }

  private buildExcelBuffer(rows: string[][]): Buffer {
    const sheet = XLSX.utils.aoa_to_sheet([[...INVENTORY_EXPORT_HEADERS], ...rows]);
    const workbook = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(workbook, sheet, SHEET_NAME);
    return Buffer.from(XLSX.write(workbook, { type: 'buffer', bookType: 'xlsx' }));
  }
}
