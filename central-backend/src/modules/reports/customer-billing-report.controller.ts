import { Controller, Get, Query, Res } from '@nestjs/common';
import { ApiOkResponse, ApiProduces, ApiTags } from '@nestjs/swagger';
import type { Response } from 'express';
import { CustomerBillingReportExportService } from './customer-billing-report-export.service';
import { CustomerBillingReportService } from './customer-billing-report.service';
import { CustomerBillingReportQueryDto } from './dto/customer-billing-report-query.dto';

@ApiTags('reports')
@Controller('reports/customer-billing')
export class CustomerBillingReportController {
  constructor(
    private readonly reportService: CustomerBillingReportService,
    private readonly exportService: CustomerBillingReportExportService,
  ) {}

  @Get()
  @ApiOkResponse({ description: 'Customer-wise billing report (JSON)' })
  async getReport(@Query() query: CustomerBillingReportQueryDto) {
    return await this.reportService.buildReport(query);
  }

  @Get('export')
  @ApiProduces('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')
  async exportReport(@Query() query: CustomerBillingReportQueryDto, @Res() res: Response) {
    const result = await this.exportService.buildExport(query);
    res.setHeader('Content-Type', result.contentType);
    res.setHeader('Content-Disposition', `attachment; filename="${result.filename}"`);
    res.send(result.buffer);
  }
}
