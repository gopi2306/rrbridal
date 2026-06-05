import { Controller, Get, Query, Res } from '@nestjs/common';
import { ApiProduces, ApiTags } from '@nestjs/swagger';
import type { Response } from 'express';
import { VendorReceiptReportQueryDto } from './dto/vendor-receipt-report-query.dto';
import { GoodsReceiptReportService } from './goods-receipt-report.service';

@ApiTags('goods-receipts')
@Controller('goods-receipts/reports')
export class GoodsReceiptReportController {
  constructor(private readonly reportService: GoodsReceiptReportService) {}

  @Get('vendor-summary/export')
  @ApiProduces('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')
  async exportVendorSummary(@Query() query: VendorReceiptReportQueryDto, @Res() res: Response) {
    const result = await this.reportService.buildVendorSummaryExport(query);
    res.setHeader('Content-Type', result.contentType);
    res.setHeader('Content-Disposition', `attachment; filename="${result.filename}"`);
    res.send(result.buffer);
  }
}
