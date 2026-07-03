import { Body, Controller, Get, Post, Query, Res } from '@nestjs/common';
import { ApiProduces, ApiTags } from '@nestjs/swagger';
import type { Response } from 'express';
import { DashboardService } from './dashboard.service';
import { StoreSalesDashboardQueryDto } from './dto/store-sales-dashboard-query.dto';
import { StoreVendorSalesDashboardQueryDto } from './dto/store-vendor-sales-dashboard-query.dto';
import { StoreVendorsSalesDashboardQueryDto } from './dto/store-vendors-sales-dashboard-query.dto';
import { StoreDashboardQueryDto } from './dto/store-dashboard-query.dto';
import { StoreDayCloseDashboardQueryDto } from './dto/store-day-close-dashboard-query.dto';
import { resolveStoreDayCloseOptions } from './dto/store-day-close-query.util';
import { StoreDayCloseExportQueryDto } from './dto/store-day-close-export-query.dto';
import { StoreOnlineSalesDashboardQueryDto } from './dto/store-online-sales-dashboard-query.dto';
import { StoreSalesmanDashboardQueryDto, StoreSalesmenDashboardQueryDto } from './dto/store-salesmen-dashboard-query.dto';
import { WarehouseDashboardQueryDto } from './dto/warehouse-dashboard-query.dto';
import { StoreDashboardService } from './store-dashboard.service';
import { StoreDayCloseDashboardService } from './store-day-close-dashboard.service';
import { StoreDayCloseReportService } from './store-day-close-report.service';
import { StoreSalesDashboardService } from './store-sales-dashboard.service';
import { StoreVendorSalesDashboardService } from './store-vendor-sales-dashboard.service';
import { StoreVendorsSalesReportService } from './store-vendors-sales-report.service';
import { StoreOnlineSalesDashboardService } from './store-online-sales-dashboard.service';
import { StoreSalesmenDashboardService } from './store-salesmen-dashboard.service';
import { WarehouseDashboardService } from './warehouse-dashboard.service';
import type { StoreSalesDashboardOptions } from './store-sales-dashboard.types';
import type { StoreVendorSalesDashboardOptions } from './store-vendor-sales-dashboard.types';
import type { StoreVendorsSalesDashboardOptions } from './store-vendors-sales-dashboard.types';
import type { StoreVendorsSalesReportOptions } from './store-vendors-sales-report.types';
import type { StoreDashboardOptions } from './store-dashboard.types';
import type { StoreOnlineSalesDashboardOptions } from './store-online-sales-dashboard.types';
import type { StoreSalesmanDashboardOptions, StoreSalesmenDashboardOptions } from './store-salesmen-dashboard.types';
import type { WarehouseDashboardOptions } from './warehouse-dashboard.types';
import { businessTodayParts } from './store-sales-payload.util';

@ApiTags('dashboard')
@Controller('dashboard')
export class DashboardController {
  constructor(
    private readonly dashboardService: DashboardService,
    private readonly warehouseDashboardService: WarehouseDashboardService,
    private readonly storeDashboardService: StoreDashboardService,
    private readonly storeSalesDashboardService: StoreSalesDashboardService,
    private readonly storeVendorSalesDashboardService: StoreVendorSalesDashboardService,
    private readonly storeVendorsSalesReportService: StoreVendorsSalesReportService,
    private readonly storeDayCloseDashboardService: StoreDayCloseDashboardService,
    private readonly storeDayCloseReportService: StoreDayCloseReportService,
    private readonly storeOnlineSalesDashboardService: StoreOnlineSalesDashboardService,
    private readonly storeSalesmenDashboardService: StoreSalesmenDashboardService,
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

  @Get('store/day-close')
  async getStoreDayClose(@Query() query: StoreDayCloseDashboardQueryDto) {
    const options = resolveStoreDayCloseOptions(query);
    return await this.storeDayCloseDashboardService.getDayCloseDashboard(options);
  }

  @Get('store/day-close/export')
  @ApiProduces('text/csv', 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')
  async exportStoreDayCloseReport(
    @Query() query: StoreDayCloseExportQueryDto,
    @Res() res: Response,
  ) {
    const dayClose = resolveStoreDayCloseOptions(query);
    const result = await this.storeDayCloseReportService.buildExport({
      ...dayClose,
      format: query.format,
    });
    res.setHeader('Content-Type', result.contentType);
    res.setHeader('Content-Disposition', `attachment; filename="${result.filename}"`);
    res.setHeader('Access-Control-Expose-Headers', 'Content-Disposition');
    res.send(result.buffer);
  }

  @Get('store/sales')
  async getStoreSalesDashboard(@Query() query: StoreSalesDashboardQueryDto) {
    const cal = businessTodayParts();
    const options: StoreSalesDashboardOptions = {
      period: query.period ?? 'today',
      year: query.year ?? cal.year,
      month: query.month ?? cal.month,
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

  @Get('store/online-sales')
  async getStoreOnlineSalesDashboard(@Query() query: StoreOnlineSalesDashboardQueryDto) {
    const cal = businessTodayParts();
    const options: StoreOnlineSalesDashboardOptions = {
      period: query.period ?? 'today',
      year: query.year ?? cal.year,
      month: query.month ?? cal.month,
      status: query.status ?? 'all',
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
    return await this.storeOnlineSalesDashboardService.getOnlineSalesDashboard(options);
  }

  @Get('store/sales/salesmen')
  async getAllSalesmenSalesDashboard(@Query() query: StoreSalesmenDashboardQueryDto) {
    const cal = businessTodayParts();
    const options: StoreSalesmenDashboardOptions = {
      period: query.period ?? 'today',
      year: query.year ?? cal.year,
      month: query.month ?? cal.month,
      invoiceLimit: query.invoiceLimit ?? 50,
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
    return await this.storeSalesmenDashboardService.getAllSalesmenDashboard(options);
  }

  @Get('store/sales/salesman')
  async getSingleSalesmanSalesDashboard(@Query() query: StoreSalesmanDashboardQueryDto) {
    const cal = businessTodayParts();
    const options: StoreSalesmanDashboardOptions = {
      salesmanId: query.salesmanId,
      period: query.period ?? 'today',
      year: query.year ?? cal.year,
      month: query.month ?? cal.month,
      invoiceLimit: query.invoiceLimit ?? 50,
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
    return await this.storeSalesmenDashboardService.getSingleSalesmanDashboard(options);
  }

  @Get('store/sales/vendors')
  async getAllVendorsSalesDashboard(@Query() query: StoreVendorsSalesDashboardQueryDto) {
    const cal = businessTodayParts();
    const options: StoreVendorsSalesDashboardOptions = {
      period: query.period ?? 'today',
      year: query.year ?? cal.year,
      month: query.month ?? cal.month,
      invoiceLimit: query.invoiceLimit ?? 50,
      returnDetailLimit: query.returnDetailLimit ?? 20,
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
    return await this.storeVendorSalesDashboardService.getAllVendorsSalesDashboard(options);
  }

  @Get('store/sales/vendors/report')
  async getStoreVendorsSalesReport(@Query() query: StoreVendorsSalesDashboardQueryDto) {
    return await this.storeVendorsSalesReportService.getAllVendorsSalesReport(
      this.toVendorsSalesReportOptions(query),
    );
  }

  @Get('store/sales/vendors/report/file')
  async getStoreVendorsSalesReportFile(@Query() query: StoreVendorsSalesDashboardQueryDto) {
    return await this.storeVendorsSalesReportService.getAllVendorsSalesReportFile(
      this.toVendorsSalesReportOptions(query),
    );
  }

  @Post('store/sales/vendors/report/file')
  async postStoreVendorsSalesReportFile(@Body() body: StoreVendorsSalesDashboardQueryDto) {
    return await this.storeVendorsSalesReportService.getAllVendorsSalesReportFile(
      this.toVendorsSalesReportOptions(body),
    );
  }

  @Get('store/sales/vendors/report/export')
  @ApiProduces('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')
  async exportStoreVendorsSalesReport(
    @Query() query: StoreVendorsSalesDashboardQueryDto,
    @Res() res: Response,
  ) {
    this.sendVendorsSalesReportExport(
      res,
      await this.storeVendorsSalesReportService.buildAllVendorsSalesReportExport(
        this.toVendorsSalesReportOptions(query),
      ),
    );
  }

  @Post('store/sales/vendors/report/export')
  @ApiProduces('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')
  async postExportStoreVendorsSalesReport(
    @Body() body: StoreVendorsSalesDashboardQueryDto,
    @Res() res: Response,
  ) {
    this.sendVendorsSalesReportExport(
      res,
      await this.storeVendorsSalesReportService.buildAllVendorsSalesReportExport(
        this.toVendorsSalesReportOptions(body),
      ),
    );
  }

  @Get('store/sales/vendor')
  async getStoreVendorSalesDashboard(@Query() query: StoreVendorSalesDashboardQueryDto) {
    const cal = businessTodayParts();
    const options: StoreVendorSalesDashboardOptions = {
      supplierId: query.supplierId,
      period: query.period ?? 'today',
      year: query.year ?? cal.year,
      month: query.month ?? cal.month,
      topProductLimit: query.topProductLimit ?? 5,
      invoiceLimit: query.invoiceLimit ?? 20,
      returnDetailLimit: query.returnDetailLimit ?? 20,
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
    return await this.storeVendorSalesDashboardService.getStoreVendorSalesDashboard(options);
  }

  private sendVendorsSalesReportExport(
    res: Response,
    result: { buffer: Buffer; contentType: string; filename: string },
  ) {
    res.setHeader('Content-Type', result.contentType);
    res.setHeader('Content-Disposition', `attachment; filename="${result.filename}"`);
    res.setHeader('Access-Control-Expose-Headers', 'Content-Disposition');
    res.send(result.buffer);
  }

  private toVendorsSalesReportOptions(
    query: StoreVendorsSalesDashboardQueryDto,
  ): StoreVendorsSalesReportOptions {
    const cal = businessTodayParts();
    const options: StoreVendorsSalesReportOptions = {
      period: query.period ?? 'today',
      year: query.year ?? cal.year,
      month: query.month ?? cal.month,
      invoiceLimit: query.invoiceLimit ?? 50,
      returnDetailLimit: query.returnDetailLimit ?? 20,
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
    return options;
  }
}
