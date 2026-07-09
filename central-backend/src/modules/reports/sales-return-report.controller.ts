import { Controller, Get, Query, Res } from '@nestjs/common';
import { ApiOkResponse, ApiProduces, ApiTags } from '@nestjs/swagger';
import type { Response } from 'express';
import { SalesReturnReportQueryDto } from './dto/sales-return-report-query.dto';
import { SalesReturnReportExportService } from './sales-return-report-export.service';
import { SalesReturnReportService } from './sales-return-report.service';

@ApiTags('reports')
@Controller('reports/sales-return')
export class SalesReturnReportController {
  constructor(
    private readonly reportService: SalesReturnReportService,
    private readonly exportService: SalesReturnReportExportService,
  ) {}

  @Get()
  @ApiOkResponse({ description: 'Sales return item-wise report (JSON)' })
  async getReport(@Query() query: SalesReturnReportQueryDto) {
    return await this.reportService.buildReport(query);
  }

  @Get('export')
  @ApiProduces('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')
  async exportReport(@Query() query: SalesReturnReportQueryDto, @Res() res: Response) {
    const result = await this.exportService.buildExport(query);
    res.setHeader('Content-Type', result.contentType);
    res.setHeader('Content-Disposition', `attachment; filename="${result.filename}"`);
    res.send(result.buffer);
  }
}
