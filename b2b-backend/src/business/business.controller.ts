import {
  Body,
  Controller,
  Delete,
  Get,
  Param,
  Patch,
  Post,
  Put,
  Query,
  UseGuards,
} from '@nestjs/common';
import { ApiOperation, ApiTags } from '@nestjs/swagger';
import {
  CustomerWriteDto,
  DateRangeQueryDto,
  InventoryAdjustmentDto,
  InventoryQueryDto,
  ProductPatchDto,
  PurchaseOrderStatusDto,
  PurchaseOrderWriteDto,
  ScopeQueryDto,
  StoreWriteDto,
} from './business.dto';
import { BusinessService } from './business.service';
import { AdminApiKeyGuard } from '../security/admin-api-key.guard';

@ApiTags('databases')
@Controller()
@UseGuards(AdminApiKeyGuard)
export class BusinessController {
  constructor(private readonly service: BusinessService) {}

  @Get('databases')
  @ApiOperation({ summary: 'List configured database sources (credentials excluded)' })
  databases() {
    return this.service.databases();
  }

  @Get('databases/health')
  health() {
    return this.service.health();
  }

  @Get('inventory')
  inventory(@Query() query: InventoryQueryDto) {
    return this.service.inventory(query);
  }

  @Get('inventory/summary')
  inventorySummary(@Query() query: InventoryQueryDto) {
    return this.service.inventorySummary(query);
  }

  @Get('inventory/products/:sku')
  inventoryDetail(@Param('sku') sku: string, @Query('databaseKey') databaseKey?: string) {
    return this.service.inventoryDetail(databaseKey, sku);
  }

  @Post('inventory/adjustments')
  adjustInventory(@Body() dto: InventoryAdjustmentDto) {
    return this.service.adjustInventory(dto);
  }

  @Get('products')
  products(@Query() query: ScopeQueryDto) {
    return this.service.products(query);
  }

  @Get('products/:idOrSku')
  productDetail(@Param('idOrSku') idOrSku: string, @Query('databaseKey') databaseKey?: string) {
    return this.service.productDetail(databaseKey, idOrSku);
  }

  @Patch('products/:id')
  patchProduct(@Param('id') id: string, @Body() dto: ProductPatchDto) {
    return this.service.patchProduct(id, dto);
  }

  @Get('customers')
  customers(@Query() query: ScopeQueryDto) {
    return this.service.customers(query);
  }

  @Get('customers/:id')
  customer(@Param('id') id: string, @Query('databaseKey') databaseKey?: string) {
    return this.service.record('customers', databaseKey, id);
  }

  @Post('customers')
  createCustomer(@Body() dto: CustomerWriteDto) {
    return this.service.createCustomer(dto);
  }

  @Put('customers/:id')
  updateCustomer(@Param('id') id: string, @Body() dto: CustomerWriteDto) {
    return this.service.updateCustomer(id, dto);
  }

  @Delete('customers/:id')
  deactivateCustomer(@Param('id') id: string, @Query('databaseKey') databaseKey?: string) {
    return this.service.deactivateCustomer(id, databaseKey);
  }

  @Get('stores')
  stores(@Query() query: ScopeQueryDto) {
    return this.service.stores(query);
  }

  @Get('stores/:id')
  store(@Param('id') id: string, @Query('databaseKey') databaseKey?: string) {
    return this.service.record('stores', databaseKey, id);
  }

  @Post('stores')
  createStore(@Body() dto: StoreWriteDto) {
    return this.service.createStore(dto);
  }

  @Put('stores/:id')
  updateStore(@Param('id') id: string, @Body() dto: StoreWriteDto) {
    return this.service.updateStore(id, dto);
  }

  @Delete('stores/:id')
  deactivateStore(@Param('id') id: string, @Query('databaseKey') databaseKey?: string) {
    return this.service.deactivateStore(id, databaseKey);
  }

  @Get('purchase-orders')
  purchaseOrders(@Query() query: ScopeQueryDto) {
    return this.service.purchaseOrders(query);
  }

  @Get('purchase-orders/:id')
  purchaseOrder(@Param('id') id: string, @Query('databaseKey') databaseKey?: string) {
    return this.service.record('purchaseorders', databaseKey, id);
  }

  @Post('purchase-orders')
  createPurchaseOrder(@Body() dto: PurchaseOrderWriteDto) {
    return this.service.createPurchaseOrder(dto);
  }

  @Put('purchase-orders/:id')
  updatePurchaseOrder(@Param('id') id: string, @Body() dto: PurchaseOrderWriteDto) {
    return this.service.updatePurchaseOrder(id, dto);
  }

  @Patch('purchase-orders/:id/status')
  setPurchaseOrderStatus(@Param('id') id: string, @Body() dto: PurchaseOrderStatusDto) {
    return this.service.setPurchaseOrderStatus(id, dto);
  }

  @Get('bills')
  bills(@Query() query: ScopeQueryDto) {
    return this.service.bills(query);
  }

  @Get('bills/:id')
  bill(@Param('id') id: string, @Query('databaseKey') databaseKey?: string) {
    return this.service.record('store_invoices', databaseKey, id);
  }

  @Get('reports/dashboard')
  reports(@Query() query: DateRangeQueryDto) {
    return this.service.reports(query);
  }
}
