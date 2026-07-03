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
  @ApiOkResponse({ description: 'Sales and purchase GST report with HSN-wise breakdown (JSON)' })
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
}
