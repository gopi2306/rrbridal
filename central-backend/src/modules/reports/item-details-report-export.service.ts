import { Injectable } from '@nestjs/common';
import {
  buildExportFilename,
  buildMultiSheetExcelBuffer,
  formatExportMoney,
  TABULAR_EXPORT_MAX_ROWS,
  type TabularExportSheet,
} from '../../common/tabular-export';
import type { ItemDetailsReportQueryDto } from './dto/item-details-report-query.dto';
import { ItemDetailsReportService } from './item-details-report.service';
import type {
  ItemDetailsReportExportResult,
  ItemDetailsReportResponse,
} from './item-details-report.types';

const PO_HEADERS = [
  'PO No',
  'PO Date',
  'Status',
  'Supplier',
  'Supplier code',
  'Branch',
  'SKU',
  'Product',
  'Brand',
  'Ordered qty',
  'Cost',
  'Net cost',
  'Net amount',
] as const;

const GRN_HEADERS = [
  'Receipt No',
  'GRN No',
  'PO No',
  'Receipt date',
  'Supplier',
  'SKU',
  'Product',
  'Brand',
  'Ordered qty',
  'Received qty',
  'Outcome',
] as const;

const SOH_HEADERS = [
  'SKU',
  'Product',
  'Brand',
  'Category',
  'Warehouse qty',
  'In transit',
  'Store qty',
  'Total SOH',
  'Sales qty',
  'Remaining qty',
  'Cost price',
  'MRP',
  'Selling price',
  'Store price',
] as const;

const SALES_HEADERS = [
  'Store',
  'Bill / return no',
  'Invoice no',
  'Bill date',
  'Return',
  'SKU',
  'Product',
  'Brand',
  'Qty',
  'Rate',
  'Amount',
  'Salesman',
  'Salesman code',
  'Payment',
] as const;

@Injectable()
export class ItemDetailsReportExportService {
  constructor(private readonly reportService: ItemDetailsReportService) {}

  async buildExport(query: ItemDetailsReportQueryDto): Promise<ItemDetailsReportExportResult> {
    const report = await this.reportService.buildFullReportForExport(query, TABULAR_EXPORT_MAX_ROWS);
    const buffer = buildMultiSheetExcelBuffer(this.buildSheets(report));
    const scope = query.storeId?.trim() || 'all-stores';
    return {
      buffer,
      contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      filename: buildExportFilename('item-details-report', scope, 'xlsx'),
    };
  }

  private buildSheets(report: ItemDetailsReportResponse): TabularExportSheet[] {
    const sheets: TabularExportSheet[] = [
      {
        name: 'PO_Lines',
        headers: PO_HEADERS,
        rows: report.purchases.poLines.map((r) => [
          r.poNo,
          r.poDate,
          r.status,
          r.supplierName,
          r.supplierCode ?? '',
          r.branchId ?? '',
          r.sku,
          r.productName,
          r.brandName ?? '',
          String(r.orderedQty),
          formatExportMoney(r.cost),
          formatExportMoney(r.netCost),
          formatExportMoney(r.netAmount),
        ]),
      },
      {
        name: 'GRN_Lines',
        headers: GRN_HEADERS,
        rows: report.purchases.grnLines.map((r) => [
          r.receiptNo,
          r.grnNumber ?? '',
          r.poNo ?? '',
          r.receiptDate,
          r.supplierName ?? '',
          r.sku,
          r.productName,
          r.brandName ?? '',
          String(r.orderedQty),
          String(r.receivedQty),
          r.outcome ?? '',
        ]),
      },
      {
        name: 'SOH',
        headers: SOH_HEADERS,
        rows: report.soh.map((r) => [
          r.sku,
          r.productName,
          r.brandName ?? '',
          r.categoryName ?? '',
          String(r.warehouseQty),
          String(r.inTransitQty),
          String(r.storeQty),
          String(r.totalSoh),
          String(r.salesQty),
          String(r.remainingQty),
          r.costPrice != null ? formatExportMoney(r.costPrice) : '',
          r.mrp != null ? formatExportMoney(r.mrp) : '',
          r.sellingPrice != null ? formatExportMoney(r.sellingPrice) : '',
          r.storePrice != null ? formatExportMoney(r.storePrice) : '',
        ]),
      },
      {
        name: 'Sales',
        headers: SALES_HEADERS,
        rows: report.sales.map((r) => [
          r.storeId,
          r.documentNo,
          r.invoiceNo,
          r.billDate,
          r.isReturn ? 'Yes' : 'No',
          r.sku,
          r.productName,
          r.brandName ?? '',
          String(r.qty),
          formatExportMoney(r.rate),
          formatExportMoney(r.amount),
          r.salesman ?? '',
          r.salesmanCode ?? '',
          r.paymentSummary ?? '',
        ]),
      },
      {
        name: 'Summary',
        headers: ['Metric', 'Value'],
        rows: this.buildSummaryRows(report),
      },
    ];
    return sheets;
  }

  private buildSummaryRows(report: ItemDetailsReportResponse): string[][] {
    const { summary, filters } = report;
    const rows: string[][] = [
      ['Generated at', report.generatedAt],
      ['From', filters.from ?? 'Start'],
      ['To', filters.to ?? 'Today'],
      ['SKU filter', filters.sku ?? ''],
      ['Search', filters.search ?? ''],
      ['Store', filters.storeId ?? 'All stores'],
      ['Brand', filters.brandId ?? ''],
      ['Supplier', filters.supplierId ?? ''],
      [],
      ['PO line count (total)', String(summary.poLineCount)],
      ['GRN line count (total)', String(summary.grnLineCount)],
      ['SOH SKU count (total)', String(summary.sohSkuCount)],
      ['Sales line count (total)', String(summary.salesLineCount)],
      ['Total ordered qty (page)', String(summary.totalOrderedQty)],
      ['Total received qty (page)', String(summary.totalReceivedQty)],
      ['Total SOH qty (page)', String(summary.totalSohQty)],
      ['Total sold qty (page)', String(summary.totalSoldQty)],
      ['Total sales amount (page)', formatExportMoney(summary.totalSalesAmount)],
    ];

    if (summary.truncated.poLines) rows.push(['PO lines truncated', 'Yes']);
    if (summary.truncated.grnLines) rows.push(['GRN lines truncated', 'Yes']);
    if (summary.truncated.soh) rows.push(['SOH truncated', 'Yes']);
    if (summary.truncated.sales) rows.push(['Sales truncated', 'Yes']);

    rows.push(
      [],
      ['Note', 'SOH is a current snapshot, not historical stock as-of-date.'],
      ['Note', `Each data sheet is capped at ${TABULAR_EXPORT_MAX_ROWS} rows.`],
    );

    return rows;
  }
}
