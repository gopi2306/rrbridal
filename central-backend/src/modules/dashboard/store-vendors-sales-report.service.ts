import { Injectable } from '@nestjs/common';
import * as XLSX from 'xlsx';
import {
  buildExportFilename,
  formatExportMarginPercent,
  formatExportMoney,
} from '../../common/tabular-export';
import { roundMoney } from '../../common/money.util';
import { StoreVendorSalesDashboardService } from './store-vendor-sales-dashboard.service';
import type { StoreVendorsSalesDashboardSummary } from './store-vendors-sales-dashboard.types';
import type {
  StoreVendorsSalesReportExportResult,
  StoreVendorsSalesReportFileResponse,
  StoreVendorsSalesReportHeaderRow,
  StoreVendorsSalesReportOptions,
  StoreVendorsSalesReportResponse,
} from './store-vendors-sales-report.types';

const VENDOR_WISE_HEADERS = [
  'Vendor',
  'Cost price',
  'Selling price',
  'Sales qty',
  'Total cost value',
  'Total S.P. value',
  'Margin',
  'Margin %',
] as const;

@Injectable()
export class StoreVendorsSalesReportService {
  constructor(private readonly vendorSalesService: StoreVendorSalesDashboardService) {}

  async getAllVendorsSalesReport(
    options: StoreVendorsSalesReportOptions,
  ): Promise<StoreVendorsSalesReportResponse> {
    const dashboard = await this.vendorSalesService.getAllVendorsSalesDashboard(options);
    const headerRows = this.buildHeaderRows(dashboard.store, dashboard.period, dashboard.summary);

    return {
      store: dashboard.store,
      period: dashboard.period,
      headerRows,
      summary: dashboard.summary,
      rows: dashboard.vendors,
      recentInvoices: dashboard.recentInvoices,
      returns: dashboard.returns,
    };
  }

  async getAllVendorsSalesReportFile(
    options: StoreVendorsSalesReportOptions,
  ): Promise<StoreVendorsSalesReportFileResponse> {
    const report = await this.getAllVendorsSalesReport(options);
    const exportResult = this.buildExportFromReport(report);
    return {
      report,
      file: {
        filename: exportResult.filename,
        contentType: exportResult.contentType,
        base64: exportResult.buffer.toString('base64'),
      },
    };
  }

  async buildAllVendorsSalesReportExport(
    options: StoreVendorsSalesReportOptions,
  ): Promise<StoreVendorsSalesReportExportResult> {
    const report = await this.getAllVendorsSalesReport(options);
    return this.buildExportFromReport(report);
  }

  private buildExportFromReport(
    report: StoreVendorsSalesReportResponse,
  ): StoreVendorsSalesReportExportResult {
    const aoa: string[][] = [
      ...report.headerRows.map((h) => [h.label, h.value]),
      [],
      [],
      [...VENDOR_WISE_HEADERS],
      ...report.rows.map((r) => [
        r.vendorName,
        formatExportMoney(r.costPrice),
        formatExportMoney(r.sellingPrice),
        String(r.salesQty),
        formatExportMoney(r.totalCostValue),
        formatExportMoney(r.totalSellingValue),
        formatExportMoney(r.margin),
        formatExportMarginPercent(r.margin, r.totalCostValue),
      ]),
      [],
      [
        'Total',
        '',
        '',
        String(report.summary.salesQty),
        formatExportMoney(report.summary.totalCostValue),
        formatExportMoney(report.summary.totalSellingValue),
        formatExportMoney(report.summary.margin),
        formatExportMarginPercent(report.summary.margin, report.summary.totalCostValue),
      ],
    ];

    const sheet = XLSX.utils.aoa_to_sheet(aoa);
    const workbook = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(workbook, sheet, 'Vendor wise');
    const buffer = Buffer.from(XLSX.write(workbook, { type: 'buffer', bookType: 'xlsx' }));

    const scopeCode = `${report.period.from}-to-${report.period.to}-${report.store.code}`;
    return {
      buffer,
      contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      filename: buildExportFilename('store-vendors-sales-report', scopeCode, 'xlsx'),
    };
  }

  private buildHeaderRows(
    store: { code: string; name: string },
    period: { from: string; to: string; label: string },
    summary: StoreVendorsSalesDashboardSummary,
  ): StoreVendorsSalesReportHeaderRow[] {
    const avgCost = summary.salesQty > 0 ? roundMoney(summary.totalCostValue / summary.salesQty) : 0;
    const avgSelling =
      summary.salesQty > 0 ? roundMoney(summary.totalSellingValue / summary.salesQty) : 0;

    return [
      { label: 'Report', value: 'Store Vendor-wise Sales Report' },
      { label: 'Store name', value: store.name },
      { label: 'Store code', value: store.code },
      { label: 'Sales date from', value: period.from },
      { label: 'Sales date to', value: period.to },
      { label: 'Period', value: period.label },
      { label: 'Vendor count', value: String(summary.vendorCount) },
      {
        label: 'Avg cost price / Avg selling price',
        value: `${avgCost} / ${avgSelling}`,
      },
      {
        label: 'Total sales qty / Margin',
        value: `${summary.salesQty} / ${summary.margin} (${summary.marginPercent}%)`,
      },
      {
        label: 'Mapped qty / Unmapped qty',
        value: `${summary.mappedSalesQty} / ${summary.unmappedSalesQty}`,
      },
    ];
  }
}
