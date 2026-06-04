import { Controller, Get, Query, Res } from '@nestjs/common';
import { ApiProduces, ApiTags } from '@nestjs/swagger';
import type { Response } from 'express';
import { MyStoreInventoryExportQueryDto } from './dto/my-store-inventory-export-query.dto';
import { MyStoreInventoryQueryDto } from './dto/my-store-inventory-query.dto';
import { MyStoreQueryDto } from './dto/my-store-query.dto';
import { MyStoreInventoryExportService } from './my-store-inventory-export.service';
import { MyStoreService } from './my-store.service';
import type { MyStoreQueryLimits } from './my-store.types';

@ApiTags('my-store')
@Controller('my-store')
export class MyStoreController {
  constructor(
    private readonly myStoreService: MyStoreService,
    private readonly myStoreInventoryExportService: MyStoreInventoryExportService,
  ) {}

  @Get('inventory/export')
  @ApiProduces(
    'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    'text/csv',
    'application/pdf',
  )
  async exportInventory(@Query() query: MyStoreInventoryExportQueryDto, @Res() res: Response) {
    const params: { format: typeof query.format; storeCode: string; search?: string } = {
      format: query.format,
      storeCode: query.storeCode,
    };
    if (query.search !== undefined && query.search !== '') params.search = query.search;

    const result = await this.myStoreInventoryExportService.buildExport(params);
    res.setHeader('Content-Type', result.contentType);
    res.setHeader('Content-Disposition', `attachment; filename="${result.filename}"`);
    res.send(result.buffer);
  }

  @Get('inventory')
  async listInventory(@Query() query: MyStoreInventoryQueryDto) {
    const params: { page: number; limit: number; search?: string } = {
      page: query.page ?? 1,
      limit: query.limit ?? 20,
    };
    if (query.search !== undefined && query.search !== '') {
      params.search = query.search;
    }
    return await this.myStoreService.listStoreInventory(query.storeCode, params);
  }

  @Get()
  async getWorkspace(@Query() query: MyStoreQueryDto) {
    const limits: MyStoreQueryLimits = {
      purchaseIndentLimit: query.purchaseIndentLimit ?? 10,
      transferInLimit: query.transferInLimit ?? 10,
      transferOutLimit: query.transferOutLimit ?? 10,
      inventoryPreviewLimit: query.inventoryPreviewLimit ?? 20,
    };
    return await this.myStoreService.getWorkspace(query.storeId, limits);
  }
}
