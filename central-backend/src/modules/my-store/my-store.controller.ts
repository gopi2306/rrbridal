import { Controller, Get, Query } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { MyStoreInventoryQueryDto } from './dto/my-store-inventory-query.dto';
import { MyStoreQueryDto } from './dto/my-store-query.dto';
import { MyStoreService } from './my-store.service';
import type { MyStoreQueryLimits } from './my-store.types';

@ApiTags('my-store')
@Controller('my-store')
export class MyStoreController {
  constructor(private readonly myStoreService: MyStoreService) {}

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
