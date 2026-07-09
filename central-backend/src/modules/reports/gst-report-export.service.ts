import { Injectable } from '@nestjs/common';
import {
  buildExportFilename,
  buildMultiSheetExcelBuffer,
  formatExportMoney,
  type TabularExportSheet,
} from '../../common/tabular-export';
import { GstReportQueryDto } from './dto/gst-report-query.dto';
import { GstReportService } from './gst-report.service';
import {
  GstItemRow,
  GstPurchaseInvoiceRow,
  GstPurchaseReportSection,
  GstRateSummaryRow,
  GstSalesInvoiceRow,
  GstSalesReportSection,
  GstSectionSummary,
  GstTaxBreakdown,
  GstHsnRow,
} from './gst-report.types';

const SUMMARY_HEADERS = [
  'Taxable Amount',
  'Tax Amount',
  'SGST Amount',
  'CGST Amount',
  'IGST Amount',
  'Total Inclusive',
  'Document Count',
] as const;

const RATE_HEADERS = [
  'GST %',
  'SGST %',
  'CGST %',
  'IGST %',
  'Taxable Amount',
  'Tax Amount',
  'SGST Amount',
  'CGST Amount',
  'IGST Amount',
  'Total Inclusive',
] as const;

const HSN_HEADERS = [
  'HSN',
  'GST %',
  'SGST %',
  'CGST %',
  'IGST %',
  'Qty',
  'Taxable Amount',
  'Tax Amount',
  'SGST Amount',
  'CGST Amount',
  'IGST Amount',
  'Total Inclusive',
] as const;

const ITEM_HEADERS = [
  'SKU',
  'Item Name',
  'HSN',
  'GST %',
  'SGST %',
  'CGST %',
  'IGST %',
  'Qty',
  'Taxable Amount',
  'Tax Amount',
  'SGST Amount',
  'CGST Amount',
  'IGST Amount',
  'Total Inclusive',
] as const;

const SALES_INVOICE_HEADERS = [
  'Store ID',
  'Document Type',
  'Document No',
  'Document Date',
  'Customer Name',
  'Inter-state',
  'Line Count',
  'GST %',
  'SGST %',
  'CGST %',
  'IGST %',
  'Taxable Amount',
  'Tax Amount',
  'SGST Amount',
  'CGST Amount',
  'IGST Amount',
  'Total Inclusive',
] as const;

const PURCHASE_INVOICE_HEADERS = [
  'GRN Number',
  'Receipt No',
  'PO No',
  'Invoice No',
  'Invoice Date',
  'Supplier',
  'Supplier GSTIN',
  'Received Qty',
  'Purchase Cost',
  'Discount',
  'GST %',
  'SGST %',
  'CGST %',
  'IGST %',
  'Taxable Amount',
  'Tax Amount',
  'SGST Amount',
  'CGST Amount',
  'IGST Amount',
  'Total Inclusive',
] as const;

@Injectable()
export class GstReportExportService {
  constructor(private readonly reportService: GstReportService) {}

  async buildExport(query: GstReportQueryDto) {
    const report = await this.reportService.buildReport(query);
    const scope = `${report.period.from}_to_${report.period.to}`;
    const sheets = [
      ...this.buildSalesSheets(report.sales),
      ...this.buildPurchaseSheets(report.purchase),
    ];

    return {
      buffer: buildMultiSheetExcelBuffer(sheets),
      contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      filename: buildExportFilename('gst-report', scope, 'xlsx'),
      report,
    };
  }

  async buildSalesExport(query: GstReportQueryDto) {
    const report = await this.reportService.buildSalesReport(query);
    const scope = `${report.period.from}_to_${report.period.to}`;
    return {
      buffer: buildMultiSheetExcelBuffer(this.buildSalesSheets(report)),
      contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      filename: buildExportFilename('gst-report-sales', scope, 'xlsx'),
      report,
    };
  }

  async buildPurchaseExport(query: GstReportQueryDto) {
    const report = await this.reportService.buildPurchaseReport(query);
    const scope = `${report.period.from}_to_${report.period.to}`;
    return {
      buffer: buildMultiSheetExcelBuffer(this.buildPurchaseSheets(report)),
      contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      filename: buildExportFilename('gst-report-purchase', scope, 'xlsx'),
      report,
    };
  }

  private buildSalesSheets(section: GstSalesReportSection): TabularExportSheet[] {
    return [
      { name: 'Sales Summary', headers: SUMMARY_HEADERS, rows: [this.summaryRow(section.summary)] },
      {
        name: 'Sales by GST Rate',
        headers: RATE_HEADERS,
        rows: section.byGstRate.map((row) => this.taxRow(row)),
      },
      {
        name: 'Sales HSN wise',
        headers: HSN_HEADERS,
        rows: section.byHsn.map((row) => this.hsnRow(row)),
      },
      {
        name: 'Sales Item wise',
        headers: ITEM_HEADERS,
        rows: section.byItem.map((row) => this.itemRow(row)),
      },
      {
        name: 'Sales Invoice wise',
        headers: SALES_INVOICE_HEADERS,
        rows: section.byInvoice.map((row) => this.salesInvoiceRow(row)),
      },
    ];
  }

  private buildPurchaseSheets(section: GstPurchaseReportSection): TabularExportSheet[] {
    return [
      { name: 'Purchase Summary', headers: SUMMARY_HEADERS, rows: [this.summaryRow(section.summary)] },
      {
        name: 'Purchase by GST Rate',
        headers: RATE_HEADERS,
        rows: section.byGstRate.map((row) => this.taxRow(row)),
      },
      {
        name: 'Purchase HSN wise',
        headers: HSN_HEADERS,
        rows: section.byHsn.map((row) => this.hsnRow(row)),
      },
      {
        name: 'Purchase Item wise',
        headers: ITEM_HEADERS,
        rows: section.byItem.map((row) => this.itemRow(row)),
      },
      {
        name: 'Purchase GRN wise',
        headers: PURCHASE_INVOICE_HEADERS,
        rows: section.byInvoice.map((row) => this.purchaseInvoiceRow(row)),
      },
    ];
  }

  private summaryRow(summary: GstSectionSummary): string[] {
    return [
      formatExportMoney(summary.taxableAmount),
      formatExportMoney(summary.taxAmount),
      formatExportMoney(summary.sgstAmount),
      formatExportMoney(summary.cgstAmount),
      formatExportMoney(summary.igstAmount),
      formatExportMoney(summary.totalInclusive),
      String(summary.documentCount),
    ];
  }

  private taxRow(row: GstRateSummaryRow | GstTaxBreakdown): string[] {
    return [
      String(row.gstPercent),
      String(row.sgstPercent),
      String(row.cgstPercent),
      String(row.igstPercent),
      formatExportMoney(row.taxableAmount),
      formatExportMoney(row.taxAmount),
      formatExportMoney(row.sgstAmount),
      formatExportMoney(row.cgstAmount),
      formatExportMoney(row.igstAmount),
      formatExportMoney(row.totalInclusive),
    ];
  }

  private hsnRow(row: GstHsnRow): string[] {
    return [
      row.hsn,
      String(row.gstPercent),
      String(row.sgstPercent),
      String(row.cgstPercent),
      String(row.igstPercent),
      String(row.qty),
      formatExportMoney(row.taxableAmount),
      formatExportMoney(row.taxAmount),
      formatExportMoney(row.sgstAmount),
      formatExportMoney(row.cgstAmount),
      formatExportMoney(row.igstAmount),
      formatExportMoney(row.totalInclusive),
    ];
  }

  private itemRow(row: GstItemRow): string[] {
    return [
      row.sku,
      row.itemName ?? '',
      row.hsn ?? '',
      String(row.gstPercent),
      String(row.sgstPercent),
      String(row.cgstPercent),
      String(row.igstPercent),
      String(row.qty),
      formatExportMoney(row.taxableAmount),
      formatExportMoney(row.taxAmount),
      formatExportMoney(row.sgstAmount),
      formatExportMoney(row.cgstAmount),
      formatExportMoney(row.igstAmount),
      formatExportMoney(row.totalInclusive),
    ];
  }

  private salesInvoiceRow(row: GstSalesInvoiceRow): string[] {
    return [
      row.storeId,
      row.documentType,
      row.documentNo,
      row.documentDate,
      row.customerName ?? '',
      row.isInterState ? 'Yes' : 'No',
      String(row.lineCount),
      String(row.gstPercent),
      String(row.sgstPercent),
      String(row.cgstPercent),
      String(row.igstPercent),
      formatExportMoney(row.taxableAmount),
      formatExportMoney(row.taxAmount),
      formatExportMoney(row.sgstAmount),
      formatExportMoney(row.cgstAmount),
      formatExportMoney(row.igstAmount),
      formatExportMoney(row.totalInclusive),
    ];
  }

  private purchaseInvoiceRow(row: GstPurchaseInvoiceRow): string[] {
    return [
      row.grnNumber ?? '',
      row.receiptNo,
      row.poNo ?? '',
      row.invoiceNo ?? '',
      row.invoiceDate ?? '',
      row.supplierName ?? '',
      row.supplierGstNumber ?? '',
      String(row.receivedQty),
      formatExportMoney(row.purchaseCost ?? 0),
      formatExportMoney(row.discountAmount ?? 0),
      String(row.gstPercent),
      String(row.sgstPercent),
      String(row.cgstPercent),
      String(row.igstPercent),
      formatExportMoney(row.taxableAmount),
      formatExportMoney(row.taxAmount),
      formatExportMoney(row.sgstAmount),
      formatExportMoney(row.cgstAmount),
      formatExportMoney(row.igstAmount),
      formatExportMoney(row.totalInclusive),
    ];
  }
}
