import { Controller, Get, Query, Res } from '@nestjs/common';
import { ApiOkResponse, ApiProduces, ApiTags } from '@nestjs/swagger';
import type { Response } from 'express';
import { ItemDetailsReportQueryDto } from './dto/item-details-report-query.dto';
import { ItemDetailsReportExportService } from './item-details-report-export.service';
import { ItemDetailsReportService } from './item-details-report.service';

@ApiTags('reports')
@Controller('reports/item-details')
export class ItemDetailsReportController {
  constructor(
    private readonly reportService: ItemDetailsReportService,
    private readonly exportService: ItemDetailsReportExportService,
  ) {}

  @Get()
  @ApiOkResponse({ description: 'Item-wise purchase, SOH, and sales report (JSON)' })
  getReport(@Query() query: ItemDetailsReportQueryDto) {
    return this.reportService.buildReport(query);
  }

  @Get('export')
  @ApiProduces('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')
  async exportReport(@Query() query: ItemDetailsReportQueryDto, @Res() res: Response) {
    const result = await this.exportService.buildExport(query);
    res.setHeader('Content-Type', result.contentType);
    res.setHeader('Content-Disposition', `attachment; filename="${result.filename}"`);
    res.send(result.buffer);
  }
}
