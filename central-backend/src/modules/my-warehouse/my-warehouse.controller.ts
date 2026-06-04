import { Controller, Get, Query } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { MyWarehouseInventoryQueryDto } from './dto/my-warehouse-inventory-query.dto';
import { MyWarehouseQueryDto } from './dto/my-warehouse-query.dto';
import { MyWarehouseService } from './my-warehouse.service';
import type { MyWarehouseQueryLimits } from './my-warehouse.types';

@ApiTags('my-warehouse')
@Controller('my-warehouse')
export class MyWarehouseController {
  constructor(private readonly myWarehouseService: MyWarehouseService) {}

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
