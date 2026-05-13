import { Body, Controller, Get, Param, Patch, Post, Query } from '@nestjs/common';
import { ApiQuery, ApiTags } from '@nestjs/swagger';
import { CreatePurchaseReturnDto } from './dto/create-purchase-return.dto';
import { FilterPurchaseReturnDto } from './dto/filter-purchase-return.dto';
import { UpdatePurchaseReturnDto } from './dto/update-purchase-return.dto';
import { PurchaseReturnsService } from './purchase-returns.service';

@ApiTags('purchase-returns')
@Controller('purchase-returns')
export class PurchaseReturnsController {
  constructor(private readonly prService: PurchaseReturnsService) {}

  @Post()
  async create(@Body() dto: CreatePurchaseReturnDto) {
    return await this.prService.create(dto);
  }

  @Post('filter')
  async filter(@Body() dto: FilterPurchaseReturnDto) {
    return await this.prService.filter(dto);
  }

  @Get(':id')
  async get(@Param('id') id: string) {
    return await this.prService.findById(id);
  }

  @Patch(':id')
  async update(@Param('id') id: string, @Body() dto: UpdatePurchaseReturnDto) {
    return await this.prService.update(id, dto);
  }

  @Get()
  @ApiQuery({ name: 'search', required: false })
  @ApiQuery({ name: 'supplierId', required: false })
  async list(@Query('search') search?: string, @Query('supplierId') supplierId?: string) {
    const params: { search?: string; supplierId?: string } = {};
    if (search) params.search = search;
    if (supplierId) params.supplierId = supplierId;
    return await this.prService.list(params);
  }
}

