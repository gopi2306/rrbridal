import { Controller, Get, Query } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { DashboardService } from './dashboard.service';
import { StoreSalesDashboardQueryDto } from './dto/store-sales-dashboard-query.dto';
import { StoreDashboardQueryDto } from './dto/store-dashboard-query.dto';
import { WarehouseDashboardQueryDto } from './dto/warehouse-dashboard-query.dto';
import { StoreDashboardService } from './store-dashboard.service';
import { StoreSalesDashboardService } from './store-sales-dashboard.service';
import { WarehouseDashboardService } from './warehouse-dashboard.service';
import type { StoreSalesDashboardOptions } from './store-sales-dashboard.types';
import type { StoreDashboardOptions } from './store-dashboard.types';
import type { WarehouseDashboardOptions } from './warehouse-dashboard.types';

@ApiTags('dashboard')
@Controller('dashboard')
export class DashboardController {
  constructor(
    private readonly dashboardService: DashboardService,
    private readonly warehouseDashboardService: WarehouseDashboardService,
    private readonly storeDashboardService: StoreDashboardService,
    private readonly storeSalesDashboardService: StoreSalesDashboardService,
  ) {}

  @Get()
  async getDashboard() {
    return await this.dashboardService.getDashboard();
  }

  @Get('warehouse')
  async getWarehouseDashboard(@Query() query: WarehouseDashboardQueryDto) {
    const options: WarehouseDashboardOptions = {
      lowStockLimit: query.lowStockLimit ?? 10,
      activityLimit: query.activityLimit ?? 10,
      inboundDays: query.inboundDays ?? 7,
    };
    if (query.locationCode !== undefined && query.locationCode !== '') {
      options.locationCode = query.locationCode;
    }
    return await this.warehouseDashboardService.getWarehouseDashboard(options);
  }

  @Get('store')
  async getStoreDashboard(@Query() query: StoreDashboardQueryDto) {
    const options: StoreDashboardOptions = {
      lowStockLimit: query.lowStockLimit ?? 10,
      activityLimit: query.activityLimit ?? 10,
      transferLimit: query.transferLimit ?? 10,
    };
    if (query.storeId !== undefined && query.storeId !== '') {
      options.storeId = query.storeId;
    }
    return await this.storeDashboardService.getStoreDashboard(options);
  }

  @Get('store/sales')
  async getStoreSalesDashboard(@Query() query: StoreSalesDashboardQueryDto) {
    const now = new Date();
    const options: StoreSalesDashboardOptions = {
      period: query.period ?? 'today',
      year: query.year ?? now.getUTCFullYear(),
      month: query.month ?? now.getUTCMonth() + 1,
      topProductLimit: query.topProductLimit ?? 5,
      returnDetailLimit: query.returnDetailLimit ?? 20,
      creditNoteLimit: query.creditNoteLimit ?? 20,
      billPage: query.billPage ?? 1,
      billLimit: query.billLimit ?? 20,
    };
    if (query.storeId !== undefined && query.storeId !== '') {
      options.storeId = query.storeId;
    }
    if (query.from !== undefined && query.from !== '') {
      options.from = query.from;
    }
    if (query.to !== undefined && query.to !== '') {
      options.to = query.to;
    }
    return await this.storeSalesDashboardService.getStoreSalesDashboard(options);
  }
}
