import { Body, Controller, Get, Param, Post, Query } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { CreateInventoryAdjustmentDto } from './dto/create-inventory-adjustment.dto';
import { FilterInventoryAdjustmentQueryDto } from './dto/filter-inventory-adjustment.dto';
import { InventoryAdjustmentsService } from './inventory-adjustments.service';

@ApiTags('inventory-adjustments')
@Controller('inventory-adjustments')
export class InventoryAdjustmentsController {
  constructor(private readonly adjustmentsService: InventoryAdjustmentsService) {}

  @Post()
  async create(@Body() dto: CreateInventoryAdjustmentDto) {
    return await this.adjustmentsService.createFromAdmin(dto);
  }

  @Get()
  async list(@Query() query: FilterInventoryAdjustmentQueryDto) {
    const params: {
      storeCode?: string;
      locationCode?: string;
      locationKind?: 'store' | 'warehouse';
      search?: string;
      page: number;
      limit: number;
    } = {
      page: query.page ?? 1,
      limit: query.limit ?? 20,
    };
    if (query.storeCode !== undefined && query.storeCode !== '') params.storeCode = query.storeCode;
    if (query.locationCode !== undefined && query.locationCode !== '') {
      params.locationCode = query.locationCode;
    }
    if (query.locationKind !== undefined) params.locationKind = query.locationKind;
    if (query.search !== undefined && query.search !== '') params.search = query.search;
    return await this.adjustmentsService.list(params);
  }

  @Get(':id')
  async get(@Param('id') id: string) {
    return await this.adjustmentsService.findById(id);
  }
}
