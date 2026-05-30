import { Controller, Get, Query, Res } from '@nestjs/common';
import { ApiProduces, ApiTags } from '@nestjs/swagger';
import type { Response } from 'express';
import { InventoryExportQueryDto } from './dto/inventory-export-query.dto';
import { InventoryExportService } from './inventory-export.service';

@ApiTags('inventory')
@Controller('inventory')
export class InventoryExportController {
  constructor(private readonly inventoryExportService: InventoryExportService) {}

  @Get('export')
  @ApiProduces(
    'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    'text/csv',
    'application/pdf',
  )
  async export(@Query() query: InventoryExportQueryDto, @Res() res: Response) {
    const params: { format: typeof query.format; search?: string; storeId?: string } = {
      format: query.format,
    };
    if (query.search !== undefined && query.search !== '') params.search = query.search;
    if (query.storeId !== undefined && query.storeId !== '') params.storeId = query.storeId;

    const result = await this.inventoryExportService.buildExport(params);
    res.setHeader('Content-Type', result.contentType);
    res.setHeader('Content-Disposition', `attachment; filename="${result.filename}"`);
    res.send(result.buffer);
  }
}
