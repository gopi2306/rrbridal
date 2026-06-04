import { Injectable } from '@nestjs/common';
import type { InventoryExportFormat } from '../inventory/dto/inventory-export-query.dto';
import { matrixToCsv } from '../inventory/export/inventory-export-columns';
import { buildExcelBuffer, buildExportFilename } from '../../common/tabular-export';
import { buildTabularPdfBuffer } from '../../common/tabular-export-pdf';
import {
  MY_WAREHOUSE_INVENTORY_EXPORT_HEADERS,
  warehouseInventoryRowsToMatrix,
} from './export/my-warehouse-inventory-export-columns';
import { MyWarehouseService } from './my-warehouse.service';

export type MyWarehouseInventoryExportResult = {
  buffer: Buffer;
  contentType: string;
  filename: string;
};

@Injectable()
export class MyWarehouseInventoryExportService {
  constructor(private readonly myWarehouseService: MyWarehouseService) {}

  async buildExport(params: {
    format: InventoryExportFormat;
    locationCode: string;
    search?: string;
  }): Promise<MyWarehouseInventoryExportResult> {
    const rows = await this.myWarehouseService.fetchAllWarehouseInventoryRows(
      params.locationCode,
      params.search,
    );
    const matrix = warehouseInventoryRowsToMatrix(rows);
    const filename = buildExportFilename(
      'warehouse-inventory',
      params.locationCode.trim().toLowerCase(),
      params.format,
    );
    const context = this.buildContextLine(params.locationCode, params.search, rows.length);

    switch (params.format) {
      case 'csv':
        return {
          buffer: matrixToCsv(MY_WAREHOUSE_INVENTORY_EXPORT_HEADERS, matrix),
          contentType: 'text/csv; charset=utf-8',
          filename,
        };
      case 'pdf':
        return {
          buffer: await buildTabularPdfBuffer(
            'Warehouse Inventory Report',
            context,
            MY_WAREHOUSE_INVENTORY_EXPORT_HEADERS,
            matrix,
          ),
          contentType: 'application/pdf',
          filename,
        };
      case 'xlsx':
      default:
        return {
          buffer: buildExcelBuffer(MY_WAREHOUSE_INVENTORY_EXPORT_HEADERS, matrix, 'Warehouse inventory'),
          contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
          filename,
        };
    }
  }

  private buildContextLine(locationCode: string, search: string | undefined, rowCount: number): string {
    const parts = [`Warehouse: ${locationCode.trim()}`];
    if (search?.trim()) parts.push(`Search: ${search.trim()}`);
    parts.push(`Generated: ${new Date().toISOString()} · ${rowCount} product(s)`);
    return parts.join(' · ');
  }
}
