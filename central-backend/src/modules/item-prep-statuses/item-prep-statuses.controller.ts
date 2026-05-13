import { Body, Controller, Get, Param, Patch, Post } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { CreateItemPrepStatusDto } from './dto/create-item-prep-status.dto';
import { UpdateItemPrepStatusDto } from './dto/update-item-prep-status.dto';
import { ItemPrepStatusesService } from './item-prep-statuses.service';

@ApiTags('item-prep-statuses')
@Controller('item-prep-statuses')
export class ItemPrepStatusesController {
  constructor(private readonly service: ItemPrepStatusesService) {}

  @Post()
  async create(@Body() dto: CreateItemPrepStatusDto) {
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
  async update(@Param('id') id: string, @Body() dto: UpdateItemPrepStatusDto) {
    return await this.service.update(id, dto);
  }
}
