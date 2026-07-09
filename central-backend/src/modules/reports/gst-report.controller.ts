import { Controller, Get, Query, Res } from '@nestjs/common';
import { ApiOkResponse, ApiProduces, ApiTags } from '@nestjs/swagger';
import type { Response } from 'express';
import { GstReportQueryDto } from './dto/gst-report-query.dto';
import { GstReportExportService } from './gst-report-export.service';
import { GstReportService } from './gst-report.service';

@ApiTags('reports')
@Controller('reports/gst')
export class GstReportController {
  constructor(
    private readonly reportService: GstReportService,
    private readonly exportService: GstReportExportService,
  ) {}

  @Get()
  @ApiOkResponse({
    description:
      'Combined sales and purchase GST report with rate, HSN, item, and invoice-wise breakdown (JSON)',
  })
  getReport(@Query() query: GstReportQueryDto) {
    return this.reportService.buildReport(query);
  }

  @Get('export')
  @ApiProduces('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')
  async exportReport(@Query() query: GstReportQueryDto, @Res() res: Response) {
    const result = await this.exportService.buildExport(query);
    res.setHeader('Content-Type', result.contentType);
    res.setHeader('Content-Disposition', `attachment; filename="${result.filename}"`);
    res.send(result.buffer);
  }

  @Get('sales')
  @ApiOkResponse({ description: 'Sales GST report with SGST/CGST split (JSON)' })
  getSalesReport(@Query() query: GstReportQueryDto) {
    return this.reportService.buildSalesReport(query);
  }

  @Get('sales/export')
  @ApiProduces('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')
  async exportSalesReport(@Query() query: GstReportQueryDto, @Res() res: Response) {
    const result = await this.exportService.buildSalesExport(query);
    res.setHeader('Content-Type', result.contentType);
    res.setHeader('Content-Disposition', `attachment; filename="${result.filename}"`);
    res.send(result.buffer);
  }

  @Get('purchase')
  @ApiOkResponse({ description: 'Purchase GST report with SGST/CGST split (JSON)' })
  getPurchaseReport(@Query() query: GstReportQueryDto) {
    return this.reportService.buildPurchaseReport(query);
  }

  @Get('purchase/export')
  @ApiProduces('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')
  async exportPurchaseReport(@Query() query: GstReportQueryDto, @Res() res: Response) {
    const result = await this.exportService.buildPurchaseExport(query);
    res.setHeader('Content-Type', result.contentType);
    res.setHeader('Content-Disposition', `attachment; filename="${result.filename}"`);
    res.send(result.buffer);
  }
}
