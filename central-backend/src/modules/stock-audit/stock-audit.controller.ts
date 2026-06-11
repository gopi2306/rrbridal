import { Controller, Get, Query, Res } from '@nestjs/common';
import { ApiProduces, ApiTags } from '@nestjs/swagger';
import type { Response } from 'express';
import { StockAuditExportQueryDto } from './dto/stock-audit-export-query.dto';
import { StockAuditQueryDto } from './dto/stock-audit-query.dto';
import { StockAuditExportService } from './stock-audit-export.service';
import { StockAuditService } from './stock-audit.service';

@ApiTags('stock-audit')
@Controller('stock-audit')
export class StockAuditController {
  constructor(
    private readonly stockAuditService: StockAuditService,
    private readonly stockAuditExportService: StockAuditExportService,
  ) {}

  @Get('export')
  @ApiProduces(
    'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    'text/csv',
    'application/pdf',
  )
  async export(@Query() query: StockAuditExportQueryDto, @Res() res: Response) {
    const params: { format: typeof query.format; storeCode: string; search?: string } = {
      format: query.format,
      storeCode: query.storeCode,
    };
    if (query.search !== undefined && query.search !== '') params.search = query.search;

    const result = await this.stockAuditExportService.buildExport(params);
    res.setHeader('Content-Type', result.contentType);
    res.setHeader('Content-Disposition', `attachment; filename="${result.filename}"`);
    res.send(result.buffer);
  }

  @Get()
  async list(@Query() query: StockAuditQueryDto) {
    const params: { page: number; limit: number; search?: string } = {
      page: query.page ?? 1,
      limit: query.limit ?? 20,
    };
    if (query.search !== undefined && query.search !== '') params.search = query.search;
    return await this.stockAuditService.listAuditLines(query.storeCode, params);
  }
}
