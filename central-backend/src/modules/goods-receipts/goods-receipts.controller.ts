import { Body, Controller, Get, Param, Patch, Post, Query } from '@nestjs/common';
import { ApiQuery, ApiTags } from '@nestjs/swagger';
import { CreateGoodsReceiptDto } from './dto/create-goods-receipt.dto';
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
  @ApiQuery({ name: 'status', required: false, enum: ['draft', 'posted'] })
  async list(@Query('search') search?: string, @Query('poNo') poNo?: string, @Query('status') status?: string) {
    const params: { search?: string; poNo?: string; status?: string } = {};
    if (search) params.search = search;
    if (poNo) params.poNo = poNo;
    if (status) params.status = status;
    return await this.grService.list(params);
  }
}

