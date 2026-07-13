import { Controller, Get, Param, Query, Res } from '@nestjs/common';
import { ApiOkResponse, ApiProduces, ApiTags } from '@nestjs/swagger';
import type { Response } from 'express';
import { SupplierWiseReportQueryDto } from './dto/supplier-wise-report-query.dto';
import { SupplierWiseReportExportService } from './supplier-wise-report-export.service';
import { SupplierWiseReportService } from './supplier-wise-report.service';

@ApiTags('inventory')
@Controller('inventory/reports/suppliers')
export class SupplierWiseReportController {
  constructor(
    private readonly reportService: SupplierWiseReportService,
    private readonly exportService: SupplierWiseReportExportService,
  ) {}

  @Get()
  @ApiOkResponse({ description: 'Supplier-wise inventory and sales report (JSON)' })
  getSupplierReport(@Query() query: SupplierWiseReportQueryDto) {
    return this.reportService.buildSupplierReport(query);
  }

  @Get('export')
  @ApiProduces('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')
  async exportSupplierReport(@Query() query: SupplierWiseReportQueryDto, @Res() res: Response) {
    const result = await this.exportService.buildSupplierExport(query);
    res.setHeader('Content-Type', result.contentType);
    res.setHeader('Content-Disposition', `attachment; filename="${result.filename}"`);
    res.send(result.buffer);
  }

  @Get(':supplierId/products')
  @ApiOkResponse({ description: 'Product-wise report for one supplier (JSON)' })
  getProductReport(
    @Param('supplierId') supplierId: string,
    @Query() query: SupplierWiseReportQueryDto,
  ) {
    return this.reportService.buildProductReport(supplierId, query);
  }

  @Get(':supplierId/products/export')
  @ApiProduces('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')
  async exportProductReport(
    @Param('supplierId') supplierId: string,
    @Query() query: SupplierWiseReportQueryDto,
    @Res() res: Response,
  ) {
    const result = await this.exportService.buildProductExport(supplierId, query);
    res.setHeader('Content-Type', result.contentType);
    res.setHeader('Content-Disposition', `attachment; filename="${result.filename}"`);
    res.send(result.buffer);
  }
}
