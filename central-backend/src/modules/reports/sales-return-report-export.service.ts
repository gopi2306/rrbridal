import { Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import type { Model } from 'mongoose';
import {
  buildExportFilename,
  buildExcelBufferFromAoa,
  formatExportMoney,
} from '../../common/tabular-export';
import {
  CompanyProfile,
  CompanyProfileDocument,
  COMPANY_PROFILE_KEY,
} from '../company-profile/schemas/company-profile.schema';
import { SalesReturnReportQueryDto } from './dto/sales-return-report-query.dto';
import { SalesReturnReportService } from './sales-return-report.service';
import type { SalesReturnReportRow } from './sales-return-report.types';
import { formatLegacyReportDate } from './sales-return-report.util';
import { buildLegacyReportDescriptionRows } from './report-export-description.util';

const HEADERS = [
  'DEPARTMENT',
  'CATEGORY',
  'SUB CATEGORY',
  'BRAND',
  'WEIGHT AND SIZE',
  'WEIGHT PER GM OR ML',
  'OFFER GROUP',
  'STATUS (CATEGORY)',
  'COLOUR',
  'Return Date',
  'Bill No(T)',
  'MSR NO(T)',
  'Customer Name',
  'Item Name',
  'Qty',
  'Selling',
  'MRP',
  'Tax %',
  'Tax Amount',
  'Return Amount',
  'Return Counter',
  'Bill Time',
  'Return Time',
] as const;

@Injectable()
export class SalesReturnReportExportService {
  constructor(
    private readonly reportService: SalesReturnReportService,
    @InjectModel(CompanyProfile.name) private readonly companyProfileModel: Model<CompanyProfileDocument>,
  ) {}

  async buildExport(query: SalesReturnReportQueryDto) {
    const report = await this.reportService.buildReport(query);
    const companyProfile = await this.companyProfileModel
      .findOne({ settingsKey: COMPANY_PROFILE_KEY })
      .lean();
    const scope = `${report.period.storeCode ?? 'all'}-${report.period.from}_to_${report.period.to}`;

    const descriptionRows = buildLegacyReportDescriptionRows({
      from: report.period.from,
      to: report.period.to,
      title: 'Sales Return - Item Wise Detailed V.1',
      companyProfile,
      ...(report.period.storeName ? { entityLabel: report.period.storeName } : {}),
    });
    const totalsRow = this.buildTotalsRow(report.totals);
    const dataRows = report.data.map((row) => this.buildRow(row));
    const sheetRows = [...descriptionRows, totalsRow, [...HEADERS], ...dataRows];
    const buffer = buildExcelBufferFromAoa(sheetRows, 'Sales Return');

    const filename = buildExportFilename('sales-return', scope, 'xlsx');
    return {
      buffer,
      contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      filename,
      report,
    };
  }

  private buildTotalsRow(totals: { qty: number; taxAmount: number; returnAmount: number }): string[] {
    const empty = Array(HEADERS.length).fill('');
    empty[14] = formatExportMoney(totals.qty);
    empty[18] = formatExportMoney(totals.taxAmount);
    empty[19] = formatExportMoney(totals.returnAmount);
    return empty;
  }

  private buildRow(row: SalesReturnReportRow): string[] {
    return [
      row.department,
      row.category,
      row.subCategory,
      row.brand,
      row.weightAndSize,
      row.weightPerGmOrMl,
      row.offerGroup,
      row.statusCategory,
      row.colour,
      formatLegacyReportDate(row.returnDate),
      row.billNo,
      row.msrNo,
      row.customerName,
      row.itemName,
      formatExportMoney(row.qty),
      formatExportMoney(row.selling),
      formatExportMoney(row.mrp),
      formatExportMoney(row.taxPercent),
      formatExportMoney(row.taxAmount),
      formatExportMoney(row.returnAmount),
      row.returnCounter,
      row.billTime,
      row.returnTime,
    ];
  }
}
