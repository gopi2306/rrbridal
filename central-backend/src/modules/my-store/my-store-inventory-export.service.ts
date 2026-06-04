import { Injectable } from '@nestjs/common';
import type { InventoryExportFormat } from '../inventory/dto/inventory-export-query.dto';
import { matrixToCsv } from '../inventory/export/inventory-export-columns';
import { buildExcelBuffer, buildExportFilename } from '../../common/tabular-export';
import { buildTabularPdfBuffer } from '../../common/tabular-export-pdf';
import {
  MY_STORE_INVENTORY_EXPORT_HEADERS,
  storeInventoryRowsToMatrix,
} from './export/my-store-inventory-export-columns';
import { MyStoreService } from './my-store.service';

export type MyStoreInventoryExportResult = {
  buffer: Buffer;
  contentType: string;
  filename: string;
};

@Injectable()
export class MyStoreInventoryExportService {
  constructor(private readonly myStoreService: MyStoreService) {}

  async buildExport(params: {
    format: InventoryExportFormat;
    storeCode: string;
    search?: string;
  }): Promise<MyStoreInventoryExportResult> {
    const rows = await this.myStoreService.fetchAllStoreInventoryRows(
      params.storeCode,
      params.search,
    );
    const matrix = storeInventoryRowsToMatrix(rows);
    const filename = buildExportFilename('store-inventory', params.storeCode.trim().toLowerCase(), params.format);
    const context = this.buildContextLine(params.storeCode, params.search, rows.length);

    switch (params.format) {
      case 'csv':
        return {
          buffer: matrixToCsv(MY_STORE_INVENTORY_EXPORT_HEADERS, matrix),
          contentType: 'text/csv; charset=utf-8',
          filename,
        };
      case 'pdf':
        return {
          buffer: await buildTabularPdfBuffer(
            'Store Inventory Report',
            context,
            MY_STORE_INVENTORY_EXPORT_HEADERS,
            matrix,
          ),
          contentType: 'application/pdf',
          filename,
        };
      case 'xlsx':
      default:
        return {
          buffer: buildExcelBuffer(MY_STORE_INVENTORY_EXPORT_HEADERS, matrix, 'Store inventory'),
          contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
          filename,
        };
    }
  }

  private buildContextLine(storeCode: string, search: string | undefined, rowCount: number): string {
    const parts = [`Store: ${storeCode.trim()}`];
    if (search?.trim()) parts.push(`Search: ${search.trim()}`);
    parts.push(`Generated: ${new Date().toISOString()} · ${rowCount} product(s)`);
    return parts.join(' · ');
  }
}
