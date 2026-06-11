import { Injectable } from '@nestjs/common';
import type { InventoryExportFormat } from '../inventory/dto/inventory-export-query.dto';
import { matrixToCsv } from '../inventory/export/inventory-export-columns';
import { buildExcelBuffer, buildExportFilename } from '../../common/tabular-export';
import { buildTabularPdfBuffer } from '../../common/tabular-export-pdf';
import {
  STOCK_AUDIT_EXPORT_HEADERS,
  stockAuditRowsToMatrix,
} from './export/stock-audit-export-columns';
import { StockAuditService } from './stock-audit.service';

export type StockAuditExportResult = {
  buffer: Buffer;
  contentType: string;
  filename: string;
};

@Injectable()
export class StockAuditExportService {
  constructor(private readonly stockAuditService: StockAuditService) {}

  async buildExport(params: {
    format: InventoryExportFormat;
    storeCode: string;
    search?: string;
  }): Promise<StockAuditExportResult> {
    const { audit, rows } = await this.stockAuditService.fetchAllAuditLines(
      params.storeCode,
      params.search,
    );
    const matrix = stockAuditRowsToMatrix(rows);
    const filename = buildExportFilename(
      'stock-audit',
      `${audit.storeId}-${audit.auditNo}`.toLowerCase(),
      params.format,
    );
    const context = this.buildContextLine(audit.auditNo, audit.storeId, params.search, rows.length);

    switch (params.format) {
      case 'csv':
        return {
          buffer: matrixToCsv(STOCK_AUDIT_EXPORT_HEADERS, matrix),
          contentType: 'text/csv; charset=utf-8',
          filename,
        };
      case 'pdf':
        return {
          buffer: await buildTabularPdfBuffer(
            'Stock Audit Report',
            context,
            STOCK_AUDIT_EXPORT_HEADERS,
            matrix,
          ),
          contentType: 'application/pdf',
          filename,
        };
      case 'xlsx':
      default:
        return {
          buffer: buildExcelBuffer(STOCK_AUDIT_EXPORT_HEADERS, matrix, 'Stock audit'),
          contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
          filename,
        };
    }
  }

  private buildContextLine(
    auditNo: string,
    storeCode: string,
    search: string | undefined,
    rowCount: number,
  ): string {
    const parts = [`Store: ${storeCode}`, `Audit: ${auditNo}`];
    if (search?.trim()) parts.push(`Search: ${search.trim()}`);
    parts.push(`Generated: ${new Date().toISOString()} · ${rowCount} line(s)`);
    return parts.join(' · ');
  }
}
