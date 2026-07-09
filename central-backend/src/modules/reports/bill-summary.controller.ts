import { Controller, Get, Query, Res } from '@nestjs/common';
import { ApiOkResponse, ApiProduces, ApiTags } from '@nestjs/swagger';
import type { Response } from 'express';
import { BillSummaryQueryDto } from './dto/bill-summary-query.dto';
import { BillSummaryExportService } from './bill-summary-export.service';
import { BillSummaryService } from './bill-summary.service';

@ApiTags('reports')
@Controller('reports/bill-summary')
export class BillSummaryReportController {
  constructor(
    private readonly reportService: BillSummaryService,
    private readonly exportService: BillSummaryExportService,
  ) {}

  @Get()
  @ApiOkResponse({ description: 'Bill summary report (JSON)' })
  async getReport(@Query() query: BillSummaryQueryDto) {
    return await this.reportService.buildReport(query);
  }

  @Get('export')
  @ApiProduces('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')
  async exportReport(@Query() query: BillSummaryQueryDto, @Res() res: Response) {
    const result = await this.exportService.buildExport(query);
    res.setHeader('Content-Type', result.contentType);
    res.setHeader('Content-Disposition', `attachment; filename="${result.filename}"`);
    res.send(result.buffer);
  }
}

