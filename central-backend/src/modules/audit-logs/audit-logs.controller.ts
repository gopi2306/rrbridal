import { Controller, Get, Param, Query } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { AuditLogsService } from './audit-logs.service';
import { ListAuditLogsQueryDto } from './dto/list-audit-logs-query.dto';

@ApiTags('audit-logs')
@Controller('audit-logs')
export class AuditLogsController {
  constructor(private readonly auditLogsService: AuditLogsService) {}

  @Get()
  async list(@Query() query: ListAuditLogsQueryDto) {
    return await this.auditLogsService.list(query);
  }

  @Get('products/:productId')
  async productHistory(
    @Param('productId') productId: string,
    @Query('page') page?: string,
    @Query('limit') limit?: string,
  ) {
    return await this.auditLogsService.listProductHistory(
      productId,
      page ? Number(page) : 1,
      limit ? Number(limit) : 50,
    );
  }

  @Get('products/by-sku/:sku')
  async productHistoryBySku(
    @Param('sku') sku: string,
    @Query('page') page?: string,
    @Query('limit') limit?: string,
  ) {
    return await this.auditLogsService.listProductHistoryBySku(
      sku,
      page ? Number(page) : 1,
      limit ? Number(limit) : 50,
    );
  }
}
