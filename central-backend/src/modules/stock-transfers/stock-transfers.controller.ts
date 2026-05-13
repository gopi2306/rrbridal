import { Body, Controller, Get, Param, Patch, Post, Query } from '@nestjs/common';
import { ApiQuery, ApiTags } from '@nestjs/swagger';
import { CreateFromPurchaseIntentDto } from './dto/create-from-purchase-intent.dto';
import { CreateStockTransferDto } from './dto/create-stock-transfer.dto';
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

  @Get()
  @ApiQuery({ name: 'search', required: false, description: 'Search by transfer number' })
  @ApiQuery({ name: 'toStoreId', required: false })
  @ApiQuery({ name: 'status', required: false })
  @ApiQuery({ name: 'purchaseIntentId', required: false })
  async list(
    @Query('search') search?: string,
    @Query('toStoreId') toStoreId?: string,
    @Query('status') status?: string,
    @Query('purchaseIntentId') purchaseIntentId?: string,
  ) {
    const params: {
      search?: string;
      toStoreId?: string;
      status?: string;
      purchaseIntentId?: string;
    } = {};
    if (search) params.search = search;
    if (toStoreId) params.toStoreId = toStoreId;
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

  @Post(':id/status')
  async setStatus(@Param('id') id: string, @Body() dto: SetStockTransferStatusDto) {
    return await this.transfersService.setStatus(id, dto.status);
  }
}
