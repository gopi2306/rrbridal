import { Controller, Get, Query, Res } from '@nestjs/common';
import { ApiProduces, ApiTags } from '@nestjs/swagger';
import type { Response } from 'express';
import { MyWarehouseInventoryExportQueryDto } from './dto/my-warehouse-inventory-export-query.dto';
import { MyWarehouseInventoryQueryDto } from './dto/my-warehouse-inventory-query.dto';
import { MyWarehouseQueryDto } from './dto/my-warehouse-query.dto';
import { MyWarehouseInventoryExportService } from './my-warehouse-inventory-export.service';
import { MyWarehouseService } from './my-warehouse.service';
import type { MyWarehouseQueryLimits } from './my-warehouse.types';

@ApiTags('my-warehouse')
@Controller('my-warehouse')
export class MyWarehouseController {
  constructor(
    private readonly myWarehouseService: MyWarehouseService,
    private readonly myWarehouseInventoryExportService: MyWarehouseInventoryExportService,
  ) {}

  @Get('inventory/export')
  @ApiProduces(
    'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    'text/csv',
    'application/pdf',
  )
  async exportInventory(@Query() query: MyWarehouseInventoryExportQueryDto, @Res() res: Response) {
    const params: { format: typeof query.format; locationCode: string; search?: string } = {
      format: query.format,
      locationCode: query.locationCode,
    };
    if (query.search !== undefined && query.search !== '') params.search = query.search;

    const result = await this.myWarehouseInventoryExportService.buildExport(params);
    res.setHeader('Content-Type', result.contentType);
    res.setHeader('Content-Disposition', `attachment; filename="${result.filename}"`);
    res.send(result.buffer);
  }

  @Get('inventory')
  async listInventory(@Query() query: MyWarehouseInventoryQueryDto) {
    const params: { page: number; limit: number; search?: string } = {
      page: query.page ?? 1,
      limit: query.limit ?? 20,
    };
    if (query.search !== undefined && query.search !== '') {
      params.search = query.search;
    }
    return await this.myWarehouseService.listWarehouseInventory(query.locationCode, params);
  }

  @Get()
  async getWorkspace(@Query() query: MyWarehouseQueryDto) {
    const limits: MyWarehouseQueryLimits = {
      goodsReceiptLimit: query.goodsReceiptLimit ?? 10,
      purchaseOrderLimit: query.purchaseOrderLimit ?? 10,
      transferOutLimit: query.transferOutLimit ?? 10,
      inventoryPreviewLimit: query.inventoryPreviewLimit ?? 20,
    };
    return await this.myWarehouseService.getWorkspace(query.locationCode, limits);
  }
}
