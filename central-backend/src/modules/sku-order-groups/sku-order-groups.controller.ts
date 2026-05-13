import { Body, Controller, Get, Param, Patch, Post } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { CreateSkuOrderGroupDto } from './dto/create-sku-order-group.dto';
import { UpdateSkuOrderGroupDto } from './dto/update-sku-order-group.dto';
import { SkuOrderGroupsService } from './sku-order-groups.service';

@ApiTags('sku-order-groups')
@Controller('sku-order-groups')
export class SkuOrderGroupsController {
  constructor(private readonly service: SkuOrderGroupsService) {}

  @Post()
  async create(@Body() dto: CreateSkuOrderGroupDto) {
    return await this.service.create(dto);
  }

  @Get()
  async list() {
    return await this.service.findAll();
  }

  @Get(':id')
  async get(@Param('id') id: string) {
    return await this.service.findById(id);
  }

  @Patch(':id')
  async update(@Param('id') id: string, @Body() dto: UpdateSkuOrderGroupDto) {
    return await this.service.update(id, dto);
  }
}
