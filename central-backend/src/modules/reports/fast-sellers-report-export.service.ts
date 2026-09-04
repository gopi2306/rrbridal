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
import { SkuSalesReportQueryDto } from './dto/sku-sales-report-query.dto';
import { buildLegacyReportDescriptionRows } from './report-export-description.util';
import type { FastSellersReportResponse } from './sku-sales-report.types';
import { FastSellersReportService } from './fast-sellers-report.service';

const HEADERS = [
  'Rank',
  'SKU',
  'Description',
  'Sold Qty',
  'Return Qty',
  'Net Qty',
  'Net Amount',
  'Available Qty',
  'Low Stock',
] as const;

@Injectable()
export class FastSellersReportExportService {
  constructor(
    private readonly reportService: FastSellersReportService,
    @InjectModel(CompanyProfile.name)
    private readonly companyProfileModel: Model<CompanyProfileDocument>,
  ) {}

  async buildExport(query: SkuSalesReportQueryDto) {
    const report = await this.reportService.buildReport(query);
    const companyProfile = await this.companyProfileModel
      .findOne({ settingsKey: COMPANY_PROFILE_KEY })
      .lean();
    const prefixRows = buildLegacyReportDescriptionRows({
      from: report.period.from,
      to: report.period.to,
      title: 'Fast Sellers Report',
      companyProfile,
      entityLabel: report.period.storeName,
    });
    const filterRows = this.filterRows(report);
    const totals = report.totals;
    const totalsRow = [
      '',
      `TOTAL (${totals.skuCount} SKUs)`,
      '',
      formatExportMoney(totals.soldQty),
      formatExportMoney(totals.returnQty),
      formatExportMoney(totals.netQty),
      formatExportMoney(totals.netAmount),
      '',
      '',
    ];
    const rows = report.data.map((row) => [
      String(row.rank),
      row.sku,
      row.description,
      formatExportMoney(row.soldQty),
      formatExportMoney(row.returnQty),
      formatExportMoney(row.netQty),
      formatExportMoney(row.netAmount),
      formatExportMoney(row.availableQty),
      row.isLowStock ? 'Yes' : 'No',
    ]);

    const workbook = XLSX.utils.book_new();
    const sheet = XLSX.utils.aoa_to_sheet([
      ...prefixRows,
      ...filterRows,
      totalsRow,
      [...HEADERS],
      ...rows,
    ]);
    XLSX.utils.book_append_sheet(workbook, sheet, 'Fast Sellers');

    const scope = `${report.period.storeCode}-${report.period.from}_to_${report.period.to}`;
    return {
      buffer: Buffer.from(XLSX.write(workbook, { type: 'buffer', bookType: 'xlsx' })),
      contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      filename: buildExportFilename('fast-sellers', scope, 'xlsx'),
      report,
    };
  }

  private filterRows(report: FastSellersReportResponse): string[][] {
    const values = [
      report.period.posCounter ? `POS Counter: ${report.period.posCounter}` : '',
      report.filters.search ? `Search: ${report.filters.search}` : '',
      report.truncated ? `TRUNCATED: showing ${report.limit} of ${report.total} SKUs` : '',
    ].filter(Boolean);
    return values.length > 0 ? [[values.join(' | ')]] : [];
  }
}
