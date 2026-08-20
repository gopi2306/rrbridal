import { Controller, Get, Query, Res } from '@nestjs/common';
import { ApiOkResponse, ApiProduces, ApiTags } from '@nestjs/swagger';
import type { Response } from 'express';
import { SkuSalesReportQueryDto } from './dto/sku-sales-report-query.dto';
import { SupplierWiseSalesReportExportService } from './supplier-wise-sales-report-export.service';
import { SupplierWiseSalesReportService } from './supplier-wise-sales-report.service';

@ApiTags('reports')
@Controller('reports/supplier-wise')
export class SupplierWiseSalesReportController {
  constructor(
    private readonly reportService: SupplierWiseSalesReportService,
    private readonly exportService: SupplierWiseSalesReportExportService,
  ) {}

  @Get()
  @ApiOkResponse({ description: 'Supplier-wise sales report (JSON)' })
  async getReport(@Query() query: SkuSalesReportQueryDto) {
    return await this.reportService.buildReport(query);
  }

  @Get('export')
  @ApiProduces('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')
  async exportReport(@Query() query: SkuSalesReportQueryDto, @Res() res: Response) {
    const result = await this.exportService.buildExport(query);
    res.setHeader('Content-Type', result.contentType);
    res.setHeader('Content-Disposition', `attachment; filename="${result.filename}"`);
    res.send(result.buffer);
  }
}
