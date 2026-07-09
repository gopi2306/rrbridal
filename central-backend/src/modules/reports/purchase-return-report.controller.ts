import { Controller, Get, Query, Res } from '@nestjs/common';
import { ApiOkResponse, ApiProduces, ApiTags } from '@nestjs/swagger';
import type { Response } from 'express';
import { PurchaseReturnReportQueryDto } from './dto/purchase-return-report-query.dto';
import { PurchaseReturnReportExportService } from './purchase-return-report-export.service';
import { PurchaseReturnReportService } from './purchase-return-report.service';

@ApiTags('reports')
@Controller('reports/purchase-return')
export class PurchaseReturnReportController {
  constructor(
    private readonly reportService: PurchaseReturnReportService,
    private readonly exportService: PurchaseReturnReportExportService,
  ) {}

  @Get()
  @ApiOkResponse({ description: 'Purchase return item-wise report (JSON)' })
  async getReport(@Query() query: PurchaseReturnReportQueryDto) {
    return await this.reportService.buildReport(query);
  }

  @Get('export')
  @ApiProduces('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')
  async exportReport(@Query() query: PurchaseReturnReportQueryDto, @Res() res: Response) {
    const result = await this.exportService.buildExport(query);
    res.setHeader('Content-Type', result.contentType);
    res.setHeader('Content-Disposition', `attachment; filename="${result.filename}"`);
    res.send(result.buffer);
  }
}
