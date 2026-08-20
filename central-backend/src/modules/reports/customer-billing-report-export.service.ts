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
import type { CustomerBillingReportResponse } from './customer-billing-report.types';
import { CustomerBillingReportService } from './customer-billing-report.service';
import { CustomerBillingReportQueryDto } from './dto/customer-billing-report-query.dto';
import { buildLegacyReportDescriptionRows } from './report-export-description.util';

const SUMMARY_HEADERS = [
  'Customer Code',
  'Customer Name',
  'Customer Phone',
  'Bill Count',
  'Qty',
  'Gross Billed',
  'Returns',
  'Net Sales',
  'Cash',
  'Card',
  'UPI',
  'Credit Note',
] as const;

const DETAIL_HEADERS = [
  'Customer Code',
  'Customer Name',
  'Customer Phone',
  'Bill Date',
  'Bill No',
  'POS Counter',
  'Qty',
  'Gross Billed',
  'Returns',
  'Net Sales',
  'Cash',
  'Card',
  'UPI',
  'Credit Note',
  'Return Audit',
] as const;

const LINE_HEADERS = [
  'Customer Code',
  'Customer Name',
  'Customer Phone',
  'Bill Date',
  'Bill No',
  'Line No',
  'SKU',
  'Description',
  'HSN',
  'Qty',
  'Rate',
  'Discount',
  'Tax',
  'Amount',
] as const;

@Injectable()
export class CustomerBillingReportExportService {
  constructor(
    private readonly reportService: CustomerBillingReportService,
    @InjectModel(CompanyProfile.name)
    private readonly companyProfileModel: Model<CompanyProfileDocument>,
  ) {}

  async buildExport(query: CustomerBillingReportQueryDto) {
    const report = await this.reportService.buildReport(query);
    const companyProfile = await this.companyProfileModel
      .findOne({ settingsKey: COMPANY_PROFILE_KEY })
      .lean();
    const prefixRows = buildLegacyReportDescriptionRows({
      from: report.period.from,
      to: report.period.to,
      title: 'Customer-Wise Billing Report',
      companyProfile,
      entityLabel: report.period.storeName,
    });
    const filterRows = this.filterRows(report);

    const summaryRows = report.data.map((row) => [
      row.customerCode,
      row.customerName,
      row.customerPhone,
      String(row.billCount),
      formatExportMoney(row.qty),
      formatExportMoney(row.grossAmount),
      formatExportMoney(row.returnAmount),
      formatExportMoney(row.netAmount),
      formatExportMoney(row.payments.cash),
      formatExportMoney(row.payments.card),
      formatExportMoney(row.payments.upi),
      formatExportMoney(row.payments.creditNote),
    ]);
    const totals = report.totals;
    const totalsRow = [
      '',
      `TOTAL (${totals.customerCount} customers)`,
      '',
      String(totals.billCount),
      formatExportMoney(totals.qty),
      formatExportMoney(totals.grossAmount),
      formatExportMoney(totals.returnAmount),
      formatExportMoney(totals.netAmount),
      formatExportMoney(totals.payments.cash),
      formatExportMoney(totals.payments.card),
      formatExportMoney(totals.payments.upi),
      formatExportMoney(totals.payments.creditNote),
    ];

    const detailRows = report.data.flatMap((customer) =>
      customer.bills.map((bill) => [
        bill.customerCode,
        bill.customerName,
        bill.customerPhone,
        bill.billDate,
        bill.billNo,
        bill.posCounter,
        formatExportMoney(bill.qty),
        formatExportMoney(bill.grossAmount),
        formatExportMoney(bill.returnAmount),
        formatExportMoney(bill.netAmount),
        formatExportMoney(bill.payments.cash),
        formatExportMoney(bill.payments.card),
        formatExportMoney(bill.payments.upi),
        formatExportMoney(bill.payments.creditNote),
        bill.returns
          .map(
            (ret) =>
              `${ret.returnNo} | ${ret.returnDate} | ${ret.returnMode || ret.kind} | ${formatExportMoney(ret.amount)}`,
          )
          .join('; '),
      ]),
    );

    const lineRows = report.data.flatMap((customer) =>
      customer.bills.flatMap((bill) =>
        bill.lines.map((line) => [
          bill.customerCode,
          bill.customerName,
          bill.customerPhone,
          bill.billDate,
          bill.billNo,
          String(line.lineNo),
          line.sku,
          line.description,
          line.hsn,
          formatExportMoney(line.qty),
          formatExportMoney(line.rate),
          formatExportMoney(line.discountAmount),
          formatExportMoney(line.taxAmount),
          formatExportMoney(line.amount),
        ]),
      ),
    );

    const workbook = XLSX.utils.book_new();
    const summarySheet = XLSX.utils.aoa_to_sheet([
      ...prefixRows,
      ...filterRows,
      totalsRow,
      [...SUMMARY_HEADERS],
      ...summaryRows,
    ]);
    const detailSheet = XLSX.utils.aoa_to_sheet([
      ...prefixRows,
      ...filterRows,
      [...DETAIL_HEADERS],
      ...detailRows,
    ]);
    const lineSheet = XLSX.utils.aoa_to_sheet([
      ...prefixRows,
      ...filterRows,
      [...LINE_HEADERS],
      ...lineRows,
    ]);
    XLSX.utils.book_append_sheet(workbook, summarySheet, 'Summary');
    XLSX.utils.book_append_sheet(workbook, detailSheet, 'Bill Details');
    XLSX.utils.book_append_sheet(workbook, lineSheet, 'Line Items');

    const scope = `${report.period.storeCode}-${report.period.from}_to_${report.period.to}`;
    return {
      buffer: Buffer.from(XLSX.write(workbook, { type: 'buffer', bookType: 'xlsx' })),
      contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      filename: buildExportFilename('customer-billing', scope, 'xlsx'),
      report,
    };
  }

  private filterRows(report: CustomerBillingReportResponse): string[][] {
    const values = [
      report.period.posCounter ? `POS Counter: ${report.period.posCounter}` : '',
      report.filters.customerSearch ? `Customer search: ${report.filters.customerSearch}` : '',
      report.filters.customerCode ? `Customer code: ${report.filters.customerCode}` : '',
      report.filters.customerPhone ? `Customer phone: ${report.filters.customerPhone}` : '',
      report.truncated ? `TRUNCATED: showing ${report.limit} of ${report.total} invoices` : '',
    ].filter(Boolean);
    return values.length > 0 ? [[values.join(' | ')]] : [];
  }
}
