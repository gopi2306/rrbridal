import { Controller, Get, Query } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { InventoryGridQueryDto } from './dto/inventory-grid-query.dto';
import { InventoryService } from './inventory.service';

@ApiTags('inventory')
@Controller('inventory')
export class InventoryController {
  constructor(private readonly inventoryService: InventoryService) {}

  @Get('grid')
  async grid(@Query() query: InventoryGridQueryDto) {
    const params: { search?: string; storeId?: string; limit: number } = { limit: query.limit ?? 200 };
    if (query.search !== undefined && query.search !== '') params.search = query.search;
    if (query.storeId !== undefined && query.storeId !== '') params.storeId = query.storeId;
    return await this.inventoryService.getWarehouseStoreGrid(params);
  }
}
