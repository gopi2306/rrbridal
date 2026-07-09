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
import { PurchaseReturnReportQueryDto } from './dto/purchase-return-report-query.dto';
import { PurchaseReturnReportService } from './purchase-return-report.service';
import type { PurchaseReturnReportRow } from './purchase-return-report.types';
import { formatLegacyReportDate } from './purchase-return-report.util';
import { buildLegacyReportDescriptionRows } from './report-export-description.util';

const HEADERS = [
  'Date',
  'Purchase Return No',
  'Supplier Name',
  'Item Name',
  'Qty',
  'CGST Amount',
  'SGST Amount / UTGST Amount',
  'IGST Amount',
  'Total Amount',
  'Additional Discount % 1',
  'Additional Discount % 2',
  'Additional Discount % 3',
  'Additional Discount Amount 1',
  'Additional Discount Amount 2',
  'Additional Discount Amount 3',
  'Additional EDU Cess %',
  'Bag Qty',
  'CESS A',
  'Company Name',
  'Division Name',
  'Edu Cess %',
  'Excise Duty %',
  'Free Qty',
  'Item Alias',
  'Item Code',
  'Load Unload Charge',
  'Location Name',
  'MRP',
  'Observation Amount',
  'Purchase No',
  'Purchase Return Reference No',
  'Retail Outlet ID',
  'Slip No',
  'Travel Expense',
] as const;

@Injectable()
export class PurchaseReturnReportExportService {
  constructor(
    private readonly reportService: PurchaseReturnReportService,
    @InjectModel(CompanyProfile.name) private readonly companyProfileModel: Model<CompanyProfileDocument>,
  ) {}

  async buildExport(query: PurchaseReturnReportQueryDto) {
    const report = await this.reportService.buildReport(query);
    const companyProfile = await this.companyProfileModel
      .findOne({ settingsKey: COMPANY_PROFILE_KEY })
      .lean();
    const scopeParts = [
      report.period.branchId,
      report.period.supplierId,
      `${report.period.from}_to_${report.period.to}`,
    ].filter(Boolean);
    const scope = scopeParts.join('-') || 'all';

    const entityLabel = report.data[0]?.companyName;
    const descriptionRows = buildLegacyReportDescriptionRows({
      from: report.period.from,
      to: report.period.to,
      title: 'PR - PR Wise Item Detailed V.1',
      companyProfile,
      ...(entityLabel ? { entityLabel } : {}),
    });
    const totalsRow = this.buildTotalsRow(report.totals);
    const dataRows = report.data.map((row) => this.buildRow(row));
    const sheetRows = [...descriptionRows, totalsRow, [...HEADERS], ...dataRows];
    const buffer = buildExcelBufferFromAoa(sheetRows, 'Purchase Return');

    const filename = buildExportFilename('purchase-return', scope, 'xlsx');
    return {
      buffer,
      contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      filename,
      report,
    };
  }

  private buildTotalsRow(totals: {
    qty: number;
    cgstAmount: number;
    sgstAmount: number;
    igstAmount: number;
    totalAmount: number;
    mrp: number;
  }): string[] {
    const empty = Array(HEADERS.length).fill('');
    empty[4] = formatExportMoney(totals.qty);
    empty[5] = formatExportMoney(totals.cgstAmount);
    empty[6] = formatExportMoney(totals.sgstAmount);
    empty[7] = formatExportMoney(totals.igstAmount);
    empty[8] = formatExportMoney(totals.totalAmount);
    empty[27] = formatExportMoney(totals.mrp);
    return empty;
  }

  private buildRow(row: PurchaseReturnReportRow): string[] {
    return [
      formatLegacyReportDate(row.date),
      String(row.purchaseReturnNoNumeric || row.purchaseReturnNo),
      row.supplierName,
      row.itemName,
      formatExportMoney(row.qty),
      formatExportMoney(row.cgstAmount),
      formatExportMoney(row.sgstAmount),
      formatExportMoney(row.igstAmount),
      formatExportMoney(row.totalAmount),
      formatExportMoney(row.additionalDiscountPercent1),
      formatExportMoney(row.additionalDiscountPercent2),
      formatExportMoney(row.additionalDiscountPercent3),
      formatExportMoney(row.additionalDiscountAmount1),
      formatExportMoney(row.additionalDiscountAmount2),
      formatExportMoney(row.additionalDiscountAmount3),
      formatExportMoney(row.additionalEduCessPercent),
      formatExportMoney(row.bagQty),
      formatExportMoney(row.cessA),
      row.companyName,
      row.divisionName,
      formatExportMoney(row.eduCessPercent),
      formatExportMoney(row.exciseDutyPercent),
      formatExportMoney(row.freeQty),
      row.itemAlias,
      row.itemCode,
      formatExportMoney(row.loadUnloadCharge),
      row.locationName,
      formatExportMoney(row.mrp),
      formatExportMoney(row.observationAmount),
      row.purchaseNo,
      String(row.purchaseReturnReferenceNo),
      row.retailOutletId,
      row.slipNo,
      formatExportMoney(row.travelExpense),
    ];
  }
}
