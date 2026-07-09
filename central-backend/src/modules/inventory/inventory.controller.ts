import { Controller, Get, Query } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { InventoryFilteredQueryDto } from './dto/inventory-filtered-query.dto';
import { InventoryGridQueryDto } from './dto/inventory-grid-query.dto';
import { InventoryProductQueryDto } from './dto/inventory-product-query.dto';
import { InventoryService } from './inventory.service';

@ApiTags('inventory')
@Controller('inventory')
export class InventoryController {
  constructor(private readonly inventoryService: InventoryService) {}

  @Get('filtered')
  async filtered(@Query() query: InventoryFilteredQueryDto) {
    const params: {
      storeCode?: string;
      search?: string;
      minQty?: number;
      maxQty?: number;
      minAgeDays?: number;
      maxAgeDays?: number;
      fromDate?: string;
      toDate?: string;
      page: number;
      limit: number;
    } = {
      page: query.page ?? 1,
      limit: query.limit ?? 200,
    };
    if (query.storeCode !== undefined && query.storeCode !== '') params.storeCode = query.storeCode;
    if (query.search !== undefined && query.search !== '') params.search = query.search;
    if (query.minQty !== undefined) params.minQty = query.minQty;
    if (query.maxQty !== undefined) params.maxQty = query.maxQty;
    if (query.minAgeDays !== undefined) params.minAgeDays = query.minAgeDays;
    if (query.maxAgeDays !== undefined) params.maxAgeDays = query.maxAgeDays;
    if (query.fromDate !== undefined && query.fromDate !== '') params.fromDate = query.fromDate;
    if (query.toDate !== undefined && query.toDate !== '') params.toDate = query.toDate;
    return await this.inventoryService.getFilteredInventory(params);
  }

  @Get('product')
  async product(@Query() query: InventoryProductQueryDto) {
    return await this.inventoryService.getProductInventoryDetail(query.code);
  }

  @Get('grid')
  async grid(@Query() query: InventoryGridQueryDto) {
    const params: { search?: string; storeId?: string; page: number; limit: number } = {
      page: query.page ?? 1,
      limit: query.limit ?? 200,
    };
    if (query.search !== undefined && query.search !== '') params.search = query.search;
    if (query.storeId !== undefined && query.storeId !== '') params.storeId = query.storeId;
    return await this.inventoryService.getWarehouseStoreGrid(params);
  }
}
