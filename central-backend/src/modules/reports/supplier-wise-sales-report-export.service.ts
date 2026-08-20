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
import type { SupplierWiseReportResponse } from './sku-sales-report.types';
import { SupplierWiseSalesReportService } from './supplier-wise-sales-report.service';

const SUMMARY_HEADERS = [
  'Supplier',
  'Product Count',
  'Sold Qty',
  'Return Qty',
  'Net Qty',
  'Sold Amount',
  'Return Amount',
  'Net Amount',
] as const;

const DETAIL_HEADERS = [
  'Supplier',
  'SKU',
  'Description',
  'Sold Qty',
  'Return Qty',
  'Net Qty',
  'Sold Amount',
  'Return Amount',
  'Net Amount',
] as const;

@Injectable()
export class SupplierWiseSalesReportExportService {
  constructor(
    private readonly reportService: SupplierWiseSalesReportService,
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
      title: 'Supplier-Wise Sales Report',
      companyProfile,
      entityLabel: report.period.storeName,
    });
    const filterRows = this.filterRows(report);

    const summaryRows = report.data.map((row) => [
      row.supplierName,
      String(row.productCount),
      formatExportMoney(row.soldQty),
      formatExportMoney(row.returnQty),
      formatExportMoney(row.netQty),
      formatExportMoney(row.soldAmount),
      formatExportMoney(row.returnAmount),
      formatExportMoney(row.netAmount),
    ]);
    const totals = report.totals;
    const totalsRow = [
      `TOTAL (${totals.supplierCount} suppliers)`,
      String(totals.skuCount),
      formatExportMoney(totals.soldQty),
      formatExportMoney(totals.returnQty),
      formatExportMoney(totals.netQty),
      formatExportMoney(totals.soldAmount),
      formatExportMoney(totals.returnAmount),
      formatExportMoney(totals.netAmount),
    ];

    const detailRows = report.data.flatMap((supplier) =>
      supplier.products.map((product) => [
        supplier.supplierName,
        product.sku,
        product.description,
        formatExportMoney(product.soldQty),
        formatExportMoney(product.returnQty),
        formatExportMoney(product.netQty),
        formatExportMoney(product.soldAmount),
        formatExportMoney(product.returnAmount),
        formatExportMoney(product.netAmount),
      ]),
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
    XLSX.utils.book_append_sheet(workbook, summarySheet, 'Summary');
    XLSX.utils.book_append_sheet(workbook, detailSheet, 'Product Details');

    const scope = `${report.period.storeCode}-${report.period.from}_to_${report.period.to}`;
    return {
      buffer: Buffer.from(XLSX.write(workbook, { type: 'buffer', bookType: 'xlsx' })),
      contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      filename: buildExportFilename('supplier-wise', scope, 'xlsx'),
      report,
    };
  }

  private filterRows(report: SupplierWiseReportResponse): string[][] {
    const values = [
      report.period.posCounter ? `POS Counter: ${report.period.posCounter}` : '',
      report.filters.search ? `Search: ${report.filters.search}` : '',
      report.truncated ? `TRUNCATED: showing ${report.limit} of ${report.total} invoices` : '',
    ].filter(Boolean);
    return values.length > 0 ? [[values.join(' | ')]] : [];
  }
}
