import { Body, Controller, Get, Param, Patch, Post, Query } from '@nestjs/common';
import { ApiQuery, ApiTags } from '@nestjs/swagger';
import { CreateFromPurchaseIntentDto } from './dto/create-from-purchase-intent.dto';
import { CreateStockTransferDto } from './dto/create-stock-transfer.dto';
import { FilterStockTransferDto } from './dto/filter-stock-transfer.dto';
import { ReceiveStockTransferDto } from './dto/receive-stock-transfer.dto';
import { SetStockTransferStatusDto } from './dto/set-stock-transfer-status.dto';
import { UpdateStockTransferDto } from './dto/update-stock-transfer.dto';
import { StockTransfersService } from './stock-transfers.service';

@ApiTags('stock-transfers')
@Controller('stock-transfers')
export class StockTransfersController {
  constructor(private readonly transfersService: StockTransfersService) {}

  @Post()
  async create(@Body() dto: CreateStockTransferDto) {
    return await this.transfersService.create(dto);
  }

  @Post('from-purchase-intent/:intentId')
  async createFromPurchaseIntent(
    @Param('intentId') intentId: string,
    @Body() dto: CreateFromPurchaseIntentDto,
  ) {
    return await this.transfersService.createFromPurchaseIntent(intentId, dto);
  }

  @Post('filter')
  async filter(@Body() dto: FilterStockTransferDto) {
    return await this.transfersService.filter(dto);
  }

  @Get()
  @ApiQuery({ name: 'search', required: false, description: 'Search by transfer number' })
  @ApiQuery({ name: 'toStoreId', required: false })
  @ApiQuery({ name: 'fromStoreId', required: false, description: 'Source store for transfer out' })
  @ApiQuery({ name: 'direction', required: false, enum: ['warehouse_to_store', 'store_to_warehouse'] })
  @ApiQuery({ name: 'status', required: false })
  @ApiQuery({ name: 'purchaseIntentId', required: false })
  async list(
    @Query('search') search?: string,
    @Query('toStoreId') toStoreId?: string,
    @Query('fromStoreId') fromStoreId?: string,
    @Query('direction') direction?: string,
    @Query('status') status?: string,
    @Query('purchaseIntentId') purchaseIntentId?: string,
  ) {
    const params: {
      search?: string;
      toStoreId?: string;
      fromStoreId?: string;
      direction?: string;
      status?: string;
      purchaseIntentId?: string;
    } = {};
    if (search) params.search = search;
    if (toStoreId) params.toStoreId = toStoreId;
    if (fromStoreId) params.fromStoreId = fromStoreId;
    if (direction) params.direction = direction;
    if (status) params.status = status;
    if (purchaseIntentId) params.purchaseIntentId = purchaseIntentId;
    return await this.transfersService.list(params);
  }

  @Get(':id')
  async get(@Param('id') id: string) {
    return await this.transfersService.findById(id);
  }

  @Patch(':id')
  async update(@Param('id') id: string, @Body() dto: UpdateStockTransferDto) {
    return await this.transfersService.update(id, dto);
  }

  @Post(':id/receive')
  async receiveAtStore(@Param('id') id: string, @Body() dto: ReceiveStockTransferDto) {
    return await this.transfersService.receiveAtStore(id, dto);
  }

  @Post(':id/status')
  async setStatus(@Param('id') id: string, @Body() dto: SetStockTransferStatusDto) {
    return await this.transfersService.setStatus(id, dto.status);
  }
}
