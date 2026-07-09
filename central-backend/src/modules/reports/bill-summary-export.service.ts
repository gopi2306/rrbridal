import { Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import type { Model } from 'mongoose';
import { roundMoney } from '../../common/money.util';
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
import { BillSummaryQueryDto } from './dto/bill-summary-query.dto';
import { BillSummaryService } from './bill-summary.service';
import type { BillSummaryReportResponse, BillSummaryRow } from './bill-summary.types';
import { buildLegacyReportDescriptionRows } from './report-export-description.util';

function getAnyBucket(report: BillSummaryReportResponse, gstPercent: number) {
  for (const row of report.data) {
    const bucket = row.gstBuckets[gstPercent];
    if (bucket) return bucket;
  }
  return undefined;
}

function baseHeaders(): string[] {
  return [
    'Bill Date',
    'Counter',
    'Purchase Bill No(T)',
    'Customer Name',
    'Total Qty',
    'Goods Value',
    'Total Discount Amount',
    'Tax Amount',
    'Bill Amount',
    'Gross Margin',
    'Cash',
    'Card',
    'Credit Note',
    'UPI',
    'Bill No',
    'RRN',
  ];
}

type BillSummaryTotals = {
  totalQty: number;
  goodsValue: number;
  discountAmount: number;
  taxAmount: number;
  billAmount: number;
  grossMargin: number;
  cashAmount: number;
  cardAmount: number;
  creditNoteAmount: number;
  upiAmount: number;
  gstBuckets: Record<
    number,
    {
      taxableAmount: number;
      taxAmount: number;
      sgstAmount: number;
      cgstAmount: number;
      igstAmount: number;
    }
  >;
};

function summarizeBillSummaryRows(
  data: BillSummaryRow[],
  gstPercents: number[],
): BillSummaryTotals {
  const totals: BillSummaryTotals = {
    totalQty: 0,
    goodsValue: 0,
    discountAmount: 0,
    taxAmount: 0,
    billAmount: 0,
    grossMargin: 0,
    cashAmount: 0,
    cardAmount: 0,
    creditNoteAmount: 0,
    upiAmount: 0,
    gstBuckets: {},
  };

  for (const p of gstPercents) {
    totals.gstBuckets[p] = {
      taxableAmount: 0,
      taxAmount: 0,
      sgstAmount: 0,
      cgstAmount: 0,
      igstAmount: 0,
    };
  }

  for (const row of data) {
    totals.totalQty = roundMoney(totals.totalQty + row.totalQty);
    totals.goodsValue = roundMoney(totals.goodsValue + row.goodsValue);
    totals.discountAmount = roundMoney(totals.discountAmount + row.discountAmount);
    totals.taxAmount = roundMoney(totals.taxAmount + row.taxAmount);
    totals.billAmount = roundMoney(totals.billAmount + row.billAmount);
    totals.grossMargin = roundMoney(totals.grossMargin + row.grossMargin);
    totals.cashAmount = roundMoney(totals.cashAmount + row.cashAmount);
    totals.cardAmount = roundMoney(totals.cardAmount + row.cardAmount);
    totals.creditNoteAmount = roundMoney(totals.creditNoteAmount + row.creditNoteAmount);
    totals.upiAmount = roundMoney(totals.upiAmount + row.upiAmount);

    for (const p of gstPercents) {
      const bucket = row.gstBuckets[p];
      if (!bucket) continue;
      const agg = totals.gstBuckets[p]!;
      agg.taxableAmount = roundMoney(agg.taxableAmount + bucket.taxableAmount);
      agg.taxAmount = roundMoney(agg.taxAmount + bucket.taxAmount);
      agg.sgstAmount = roundMoney(agg.sgstAmount + bucket.sgstAmount);
      agg.cgstAmount = roundMoney(agg.cgstAmount + bucket.cgstAmount);
      agg.igstAmount = roundMoney(agg.igstAmount + bucket.igstAmount);
    }
  }

  return totals;
}

@Injectable()
export class BillSummaryExportService {
  constructor(
    private readonly reportService: BillSummaryService,
    @InjectModel(CompanyProfile.name) private readonly companyProfileModel: Model<CompanyProfileDocument>,
  ) {}

  async buildExport(query: BillSummaryQueryDto) {
    const report = await this.reportService.buildReport(query);
    const companyProfile = await this.companyProfileModel
      .findOne({ settingsKey: COMPANY_PROFILE_KEY })
      .lean();
    const scope = `${report.period.storeCode ?? 'all'}-${report.period.from}_to_${report.period.to}`;
    const gstPercents = report.gstPercents;
    const headers = this.buildHeaders(gstPercents, report);
    const dataRows = report.data.map((r) => this.buildRow(r, gstPercents));
    const totals = summarizeBillSummaryRows(report.data, gstPercents);
    const totalsRow = this.buildTotalsRow(totals, gstPercents, headers.length);
    const descriptionRows = buildLegacyReportDescriptionRows({
      from: report.period.from,
      to: report.period.to,
      title: 'Sales - Bill Wise Detailed V.1',
      companyProfile,
      ...(report.period.storeName ? { entityLabel: report.period.storeName } : {}),
    });
    const sheetRows = [
      ...descriptionRows,
      totalsRow,
      headers,
      ...dataRows,
    ];
    const buffer = buildExcelBufferFromAoa(sheetRows, 'Bill Summary');

    const filename = buildExportFilename('bill-summary', scope, 'xlsx');
    return {
      buffer,
      contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      filename,
      report,
    };
  }

  private buildTotalsRow(
    totals: BillSummaryTotals,
    gstPercents: number[],
    columnCount: number,
  ): string[] {
    const row = Array(columnCount).fill('');
    row[4] = formatExportMoney(totals.totalQty);
    row[5] = formatExportMoney(totals.goodsValue);
    row[6] = formatExportMoney(totals.discountAmount);
    row[7] = formatExportMoney(totals.taxAmount);
    row[8] = formatExportMoney(totals.billAmount);
    row[9] = formatExportMoney(totals.grossMargin);
    row[10] = formatExportMoney(totals.cashAmount);
    row[11] = formatExportMoney(totals.cardAmount);
    row[12] = formatExportMoney(totals.creditNoteAmount);
    row[13] = formatExportMoney(totals.upiAmount);

    let col = baseHeaders().length;
    for (const p of gstPercents) {
      const bucket = totals.gstBuckets[p];
      row[col++] = formatExportMoney(bucket?.taxableAmount ?? 0);
      row[col++] = formatExportMoney(bucket?.taxAmount ?? 0);
      row[col++] = formatExportMoney(bucket?.sgstAmount ?? 0);
      row[col++] = formatExportMoney(bucket?.cgstAmount ?? 0);
      row[col++] = formatExportMoney(bucket?.igstAmount ?? 0);
    }

    return row;
  }

  private buildHeaders(gstPercents: number[], report: BillSummaryReportResponse): string[] {
    const headers: string[] = [...baseHeaders()];
    for (const p of gstPercents) {
      const bucket = getAnyBucket(report, p);
      const sgstPercent = bucket?.sgstPercent ?? p / 2;
      const cgstPercent = bucket?.cgstPercent ?? p / 2;
      const igstPercent = bucket?.igstPercent ?? 0;

      headers.push(`PURCHASE ${p}% AMT`);
      headers.push(`TAX AMT ${p}%`);
      headers.push(`TAX ${sgstPercent}% SGST`);
      headers.push(`TAX ${cgstPercent}% CGST`);
      headers.push(`TAX ${igstPercent}% IGST`);
    }
    return headers;
  }

  private buildRow(row: BillSummaryRow, gstPercents: number[]): string[] {
    const base = [
      row.billDate,
      row.counter,
      row.purchaseBillNo,
      row.customerName,
      String(row.totalQty),
      formatExportMoney(row.goodsValue),
      formatExportMoney(row.discountAmount),
      formatExportMoney(row.taxAmount),
      formatExportMoney(row.billAmount),
      formatExportMoney(row.grossMargin),
      formatExportMoney(row.cashAmount),
      formatExportMoney(row.cardAmount),
      formatExportMoney(row.creditNoteAmount),
      formatExportMoney(row.upiAmount),
      row.billNo,
      row.rrn,
    ];

    const gstCols = gstPercents.flatMap((p) => {
      const b = row.gstBuckets[p];
      return [
        formatExportMoney(b?.taxableAmount ?? 0),
        formatExportMoney(b?.taxAmount ?? 0),
        formatExportMoney(b?.sgstAmount ?? 0),
        formatExportMoney(b?.cgstAmount ?? 0),
        formatExportMoney(b?.igstAmount ?? 0),
      ];
    });

    return [...base, ...gstCols];
  }
}
