import { Body, Controller, Get, Param, Patch, Post, Query } from '@nestjs/common';
import { ApiQuery, ApiTags } from '@nestjs/swagger';
import { CreateGoodsReceiptDto } from './dto/create-goods-receipt.dto';
import { FilterGoodsReceiptDto } from './dto/filter-goods-receipt.dto';
import { UpdateGoodsReceiptDto } from './dto/update-goods-receipt.dto';
import { GoodsReceiptsService } from './goods-receipts.service';

@ApiTags('goods-receipts')
@Controller('goods-receipts')
export class GoodsReceiptsController {
  constructor(private readonly grService: GoodsReceiptsService) {}

  @Post()
  async create(@Body() dto: CreateGoodsReceiptDto) {
    return await this.grService.create(dto);
  }

  @Post('filter')
  async filter(@Body() dto: FilterGoodsReceiptDto) {
    return await this.grService.filter(dto);
  }

  @Get(':id')
  async get(@Param('id') id: string) {
    return await this.grService.findById(id);
  }

  @Patch(':id')
  async update(@Param('id') id: string, @Body() dto: UpdateGoodsReceiptDto) {
    return await this.grService.update(id, dto);
  }

  @Post(':id/post')
  async postToInventory(@Param('id') id: string) {
    return await this.grService.postToInventory(id);
  }

  @Get()
  @ApiQuery({ name: 'search', required: false })
  @ApiQuery({ name: 'poNo', required: false })
  @ApiQuery({ name: 'grnNumber', required: false })
  @ApiQuery({ name: 'status', required: false, enum: ['draft', 'posted'] })
  async list(
    @Query('search') search?: string,
    @Query('poNo') poNo?: string,
    @Query('grnNumber') grnNumber?: string,
    @Query('status') status?: string,
  ) {
    const params: { search?: string; poNo?: string; grnNumber?: string; status?: string } = {};
    if (search) params.search = search;
    if (poNo) params.poNo = poNo;
    if (grnNumber) params.grnNumber = grnNumber;
    if (status) params.status = status;
    return await this.grService.list(params);
  }
}

