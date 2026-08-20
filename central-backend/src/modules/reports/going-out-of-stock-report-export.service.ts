import { Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import type { Model } from 'mongoose';
import * as XLSX from 'xlsx';
import { buildExportFilename, formatExportMoney } from '../../common/tabular-export';
import {
  CompanyProfile,
  CompanyProfileDocument,
  COMPANY_PROFILE_KEY,
} from '../company-profile/schemas/company-profile.schema';
import { GoingOutOfStockReportQueryDto } from './dto/going-out-of-stock-report-query.dto';
import type { GoingOutOfStockReportResponse } from './going-out-of-stock-report.util';
import { GoingOutOfStockReportService } from './going-out-of-stock-report.service';
import { buildLegacyReportDescriptionRows } from './report-export-description.util';

const HEADERS = [
  'SKU',
  'Product Name',
  'Store Qty',
  'Threshold',
  'Status',
  'Supplier',
] as const;

@Injectable()
export class GoingOutOfStockReportExportService {
  constructor(
    private readonly reportService: GoingOutOfStockReportService,
    @InjectModel(CompanyProfile.name)
    private readonly companyProfileModel: Model<CompanyProfileDocument>,
  ) {}

  async buildExport(query: GoingOutOfStockReportQueryDto) {
    const report = await this.reportService.buildReport(query);
    const companyProfile = await this.companyProfileModel
      .findOne({ settingsKey: COMPANY_PROFILE_KEY })
      .lean();
    const prefixRows = buildLegacyReportDescriptionRows({
      from: report.period.asOf,
      to: report.period.asOf,
      title: 'Going Out Of Stock Report',
      companyProfile,
      entityLabel: report.period.storeName,
    });
    const filterRows = this.filterRows(report);
    const totals = report.totals;
    const totalsRow = [
      `TOTAL (${totals.skuCount} SKUs)`,
      `${totals.criticalCount} critical · ${totals.lowCount} low · ${totals.zeroQtyCount} at zero`,
      '',
      '',
      '',
      '',
    ];
    const rows = report.data.map((row) => [
      row.sku,
      row.productName,
      formatExportMoney(row.storeQty),
      formatExportMoney(row.threshold),
      row.status,
      row.supplierName,
    ]);

    const workbook = XLSX.utils.book_new();
    const sheet = XLSX.utils.aoa_to_sheet([
      ...prefixRows,
      ...filterRows,
      totalsRow,
      [...HEADERS],
      ...rows,
    ]);
    XLSX.utils.book_append_sheet(workbook, sheet, 'Going Out Of Stock');

    const scope = `${report.period.storeCode}-${report.period.asOf}`;
    return {
      buffer: Buffer.from(XLSX.write(workbook, { type: 'buffer', bookType: 'xlsx' })),
      contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      filename: buildExportFilename('going-out-of-stock', scope, 'xlsx'),
      report,
    };
  }

  private filterRows(report: GoingOutOfStockReportResponse): string[][] {
    const values = [
      report.filters.search ? `Search: ${report.filters.search}` : '',
      report.filters.status ? `Status: ${report.filters.status}` : '',
      report.truncated ? `TRUNCATED: showing ${report.limit} of ${report.total} SKUs` : '',
    ].filter(Boolean);
    return values.length > 0 ? [[values.join(' | ')]] : [];
  }
}
