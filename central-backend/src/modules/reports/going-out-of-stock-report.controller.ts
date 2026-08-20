import { Controller, Get, Query, Res } from '@nestjs/common';
import { ApiOkResponse, ApiProduces, ApiTags } from '@nestjs/swagger';
import type { Response } from 'express';
import { GoingOutOfStockReportQueryDto } from './dto/going-out-of-stock-report-query.dto';
import { GoingOutOfStockReportExportService } from './going-out-of-stock-report-export.service';
import { GoingOutOfStockReportService } from './going-out-of-stock-report.service';

@ApiTags('reports')
@Controller('reports/going-out-of-stock')
export class GoingOutOfStockReportController {
  constructor(
    private readonly reportService: GoingOutOfStockReportService,
    private readonly exportService: GoingOutOfStockReportExportService,
  ) {}

  @Get()
  @ApiOkResponse({ description: 'Going out of stock report (JSON)' })
  async getReport(@Query() query: GoingOutOfStockReportQueryDto) {
    return await this.reportService.buildReport(query);
  }

  @Get('export')
  @ApiProduces('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')
  async exportReport(@Query() query: GoingOutOfStockReportQueryDto, @Res() res: Response) {
    const result = await this.exportService.buildExport(query);
    res.setHeader('Content-Type', result.contentType);
    res.setHeader('Content-Disposition', `attachment; filename="${result.filename}"`);
    res.send(result.buffer);
  }
}
