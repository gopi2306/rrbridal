import { Injectable } from '@nestjs/common';
import {
  buildExportFilename,
  buildMultiSheetExcelBuffer,
  formatExportMoney,
  type TabularExportSheet,
} from '../../common/tabular-export';
import { GstReportQueryDto } from './dto/gst-report-query.dto';
import { GstReportService } from './gst-report.service';
import { GstReportResult } from './gst-report.types';

const SUMMARY_HEADERS = [
  'Section',
  'Taxable Amount',
  'Tax Amount',
  'Total Inclusive',
  'Document Count',
] as const;

const RATE_HEADERS = [
  'Section',
  'GST %',
  'Taxable Amount',
  'Tax Amount',
  'Total Inclusive',
] as const;

const HSN_HEADERS = [
  'Section',
  'HSN',
  'GST %',
  'Qty',
  'Taxable Amount',
  'Tax Amount',
  'Total Inclusive',
] as const;

@Injectable()
export class GstReportExportService {
  constructor(private readonly reportService: GstReportService) {}

  async buildExport(query: GstReportQueryDto) {
    const report = await this.reportService.buildReport(query);
    const scope = `${report.period.from}_to_${report.period.to}`;
    const sheets: TabularExportSheet[] = [
      {
        name: 'Summary',
        headers: SUMMARY_HEADERS,
        rows: [
          this.summaryRow('Sales', report.sales.summary),
          this.summaryRow('Purchase', report.purchase.summary),
        ],
      },
      {
        name: 'Sales by GST Rate',
        headers: RATE_HEADERS,
        rows: report.sales.byGstRate.map((row) => [
          'Sales',
          String(row.gstPercent),
          formatExportMoney(row.taxableAmount),
          formatExportMoney(row.taxAmount),
          formatExportMoney(row.totalInclusive),
        ]),
      },
      {
        name: 'Sales HSN wise',
        headers: HSN_HEADERS,
        rows: report.sales.byHsn.map((row) => this.hsnRow('Sales', row)),
      },
      {
        name: 'Purchase by GST Rate',
        headers: RATE_HEADERS,
        rows: report.purchase.byGstRate.map((row) => [
          'Purchase',
          String(row.gstPercent),
          formatExportMoney(row.taxableAmount),
          formatExportMoney(row.taxAmount),
          formatExportMoney(row.totalInclusive),
        ]),
      },
      {
        name: 'Purchase HSN wise',
        headers: HSN_HEADERS,
        rows: report.purchase.byHsn.map((row) => this.hsnRow('Purchase', row)),
      },
    ];

    return {
      buffer: buildMultiSheetExcelBuffer(sheets),
      contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      filename: buildExportFilename('gst-report', scope, 'xlsx'),
      report,
    };
  }

  private summaryRow(
    section: string,
    summary: GstReportResult['sales']['summary'],
  ): string[] {
    return [
      section,
      formatExportMoney(summary.taxableAmount),
      formatExportMoney(summary.taxAmount),
      formatExportMoney(summary.totalInclusive),
      String(summary.documentCount),
    ];
  }

  private hsnRow(
    section: string,
    row: GstReportResult['sales']['byHsn'][number],
  ): string[] {
    return [
      section,
      row.hsn,
      String(row.gstPercent),
      String(row.qty),
      formatExportMoney(row.taxableAmount),
      formatExportMoney(row.taxAmount),
      formatExportMoney(row.totalInclusive),
    ];
  }
}
