import { Body, Controller, Get, Param, Patch, Post, Query } from '@nestjs/common';
import { ApiQuery, ApiTags } from '@nestjs/swagger';
import { CreatePurchaseOrderDto } from './dto/create-purchase-order.dto';
import { UpdatePurchaseOrderDto } from './dto/update-purchase-order.dto';
import { PurchaseOrdersService } from './purchase-orders.service';

@ApiTags('purchase-orders')
@Controller('purchase-orders')
export class PurchaseOrdersController {
  constructor(private readonly poService: PurchaseOrdersService) {}

  @Post()
  async create(@Body() dto: CreatePurchaseOrderDto) {
    return await this.poService.create(dto);
  }

  @Get(':id')
  async get(@Param('id') id: string) {
    return await this.poService.findById(id);
  }

  @Patch(':id')
  async update(@Param('id') id: string, @Body() dto: UpdatePurchaseOrderDto) {
    return await this.poService.update(id, dto);
  }

  @Post(':id/approve')
  async approve(@Param('id') id: string) {
    return await this.poService.setStatus(id, 'approved');
  }

  @Post(':id/close')
  async close(@Param('id') id: string) {
    return await this.poService.setStatus(id, 'closed');
  }

  @Post(':id/receive')
  async receive(@Param('id') id: string) {
    // scaffold: mark as received. Later: accept received quantities per line and compute partial status.
    return await this.poService.setStatus(id, 'received');
  }

  @Get()
  @ApiQuery({ name: 'search', required: false, description: 'Search by PO number or supplier name' })
  @ApiQuery({ name: 'supplierId', required: false })
  @ApiQuery({ name: 'status', required: false })
  async list(@Query('search') search?: string, @Query('supplierId') supplierId?: string, @Query('status') status?: string) {
    const params: { search?: string; supplierId?: string; status?: string } = {};
    if (search) params.search = search;
    if (supplierId) params.supplierId = supplierId;
    if (status) params.status = status;
    return await this.poService.list(params);
  }
}

