import { Injectable } from '@nestjs/common';
import {
  buildExportFilename,
  buildExcelBufferFromAoa,
  formatExportMarginPercent,
  formatExportMoney,
} from '../../common/tabular-export';
import type { SupplierWiseReportQueryDto } from './dto/supplier-wise-report-query.dto';
import { SupplierWiseReportService } from './supplier-wise-report.service';
import type {
  SupplierWiseProductReportResponse,
  SupplierWiseReportExportResult,
  SupplierWiseReportResponse,
} from './supplier-wise-report.types';

const SUPPLIER_HEADERS = [
  'Supplier Name',
  'Stock Qty',
  'Products',
  'Cost Value',
  'Selling Value',
  'Margin',
  'Margin %',
] as const;

const PRODUCT_HEADERS = [
  'SKU',
  'Product',
  'Stock Qty',
  'Cost Value',
  'Selling Value',
  'Margin',
  'Margin %',
] as const;

@Injectable()
export class SupplierWiseReportExportService {
  constructor(private readonly reportService: SupplierWiseReportService) {}

  async buildSupplierExport(query: SupplierWiseReportQueryDto): Promise<SupplierWiseReportExportResult> {
    const report = await this.reportService.buildSupplierReport(query);
    return this.buildSupplierExportFromReport(report);
  }

  async buildProductExport(
    supplierId: string,
    query: SupplierWiseReportQueryDto,
  ): Promise<SupplierWiseReportExportResult> {
    const report = await this.reportService.buildProductReport(supplierId, query);
    return this.buildProductExportFromReport(report);
  }

  buildSupplierExportFromReport(report: SupplierWiseReportResponse): SupplierWiseReportExportResult {
    const aoa: string[][] = [
      ['Scope', report.filters.scope],
      ['Store', report.filters.store.label],
      [],
      [...SUPPLIER_HEADERS],
      ...report.rows.map((r) => [
        r.supplierName,
        String(r.stockQty),
        String(r.productCount),
        formatExportMoney(r.costValue),
        formatExportMoney(r.sellingValue),
        formatExportMoney(r.margin),
        formatExportMarginPercent(r.margin, r.costValue),
      ]),
      [],
      [
        'Total',
        String(report.summary.stockQty),
        String(report.summary.productCount),
        formatExportMoney(report.summary.totalCostValue),
        formatExportMoney(report.summary.totalSellingValue),
        formatExportMoney(report.summary.totalMargin),
        formatExportMarginPercent(report.summary.totalMargin, report.summary.totalCostValue),
      ],
    ];

    return this.toExportResult(
      aoa,
      'Supplier wise',
      report.filters.scope,
      report.filters.store.code,
      'supplier-wise-report',
    );
  }

  buildProductExportFromReport(report: SupplierWiseProductReportResponse): SupplierWiseReportExportResult {
    const aoa: string[][] = [
      ['Supplier', report.supplier.name],
      ['Scope', report.filters.scope],
      ['Store', report.filters.store.label],
      [],
      [...PRODUCT_HEADERS],
      ...report.rows.map((r) => [
        r.sku,
        r.productName,
        String(r.stockQty),
        formatExportMoney(r.costValue),
        formatExportMoney(r.sellingValue),
        formatExportMoney(r.margin),
        formatExportMarginPercent(r.margin, r.costValue),
      ]),
      [],
      [
        'Total',
        '',
        String(report.summary.stockQty),
        formatExportMoney(report.summary.costValue),
        formatExportMoney(report.summary.sellingValue),
        formatExportMoney(report.summary.margin),
        formatExportMarginPercent(report.summary.margin, report.summary.costValue),
      ],
    ];

    return this.toExportResult(
      aoa,
      'Products',
      report.filters.scope,
      report.filters.store.code,
      `supplier-products-${report.supplier.id}`,
    );
  }

  private toExportResult(
    aoa: string[][],
    sheetName: string,
    scope: string,
    storeCode: string,
    prefix: string,
  ): SupplierWiseReportExportResult {
    const scopeCode = `${scope}-${storeCode}`;
    return {
      buffer: buildExcelBufferFromAoa(aoa, sheetName),
      contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      filename: buildExportFilename(prefix, scopeCode, 'xlsx'),
    };
  }
}
