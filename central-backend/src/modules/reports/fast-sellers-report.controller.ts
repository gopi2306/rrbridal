import { Controller, Get, Query, Res } from '@nestjs/common';
import { ApiOkResponse, ApiProduces, ApiTags } from '@nestjs/swagger';
import type { Response } from 'express';
import { SkuSalesReportQueryDto } from './dto/sku-sales-report-query.dto';
import { FastSellersReportExportService } from './fast-sellers-report-export.service';
import { FastSellersReportService } from './fast-sellers-report.service';

@ApiTags('reports')
@Controller('reports/fast-sellers')
export class FastSellersReportController {
  constructor(
    private readonly reportService: FastSellersReportService,
    private readonly exportService: FastSellersReportExportService,
  ) {}

  @Get()
  @ApiOkResponse({ description: 'Fast sellers report (JSON)' })
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
