import { Controller, Get, Query } from '@nestjs/common';
import { ApiOkResponse, ApiTags } from '@nestjs/swagger';
import { InventoryFilteredQueryDto } from './dto/inventory-filtered-query.dto';
import { InventoryFilteredSummaryQueryDto } from './dto/inventory-filtered-summary-query.dto';
import { InventoryGridQueryDto } from './dto/inventory-grid-query.dto';
import { InventoryProductQueryDto } from './dto/inventory-product-query.dto';
import {
  FilteredInventoryParams,
  FilteredInventorySummary,
  InventoryService,
} from './inventory.service';

type FilteredInventoryFilterQuery = InventoryFilteredSummaryQueryDto;

function toFilteredInventoryFilterParams(
  query: FilteredInventoryFilterQuery,
): Omit<FilteredInventoryParams, 'page' | 'limit'> {
  const params: Omit<FilteredInventoryParams, 'page' | 'limit'> = {};
  if (query.storeCode !== undefined && query.storeCode !== '') params.storeCode = query.storeCode;
  if (query.search !== undefined && query.search !== '') params.search = query.search;
  if (query.departmentId !== undefined && query.departmentId !== '') params.departmentId = query.departmentId;
  if (query.categoryId !== undefined && query.categoryId !== '') params.categoryId = query.categoryId;
  if (query.subCategoryId !== undefined && query.subCategoryId !== '') params.subCategoryId = query.subCategoryId;
  const supplierId = query.supplierId ?? query.supplierNameId;
  if (supplierId !== undefined && supplierId !== '') params.supplierId = supplierId;
  if (query.minQty !== undefined) params.minQty = query.minQty;
  if (query.maxQty !== undefined) params.maxQty = query.maxQty;
  if (query.minAgeDays !== undefined) params.minAgeDays = query.minAgeDays;
  if (query.maxAgeDays !== undefined) params.maxAgeDays = query.maxAgeDays;
  if (query.fromDate !== undefined && query.fromDate !== '') params.fromDate = query.fromDate;
  if (query.toDate !== undefined && query.toDate !== '') params.toDate = query.toDate;
  return params;
}

function toFilteredInventoryParams(query: InventoryFilteredQueryDto): FilteredInventoryParams {
  return {
    ...toFilteredInventoryFilterParams(query),
    page: query.page ?? 1,
    limit: query.limit ?? 200,
  };
}

@ApiTags('inventory')
@Controller('inventory')
export class InventoryController {
  constructor(private readonly inventoryService: InventoryService) {}

  @Get('filtered/summary')
  @ApiOkResponse({ description: 'Filtered inventory summary totals (JSON)' })
  async filteredSummary(
    @Query() query: InventoryFilteredSummaryQueryDto,
  ): Promise<FilteredInventorySummary> {
    return await this.inventoryService.getFilteredInventorySummary(toFilteredInventoryFilterParams(query));
  }

  @Get('filtered')
  async filtered(@Query() query: InventoryFilteredQueryDto) {
    return await this.inventoryService.getFilteredInventory(toFilteredInventoryParams(query));
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
