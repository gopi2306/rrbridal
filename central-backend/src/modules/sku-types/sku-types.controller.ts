import { Body, Controller, Get, Param, Patch, Post } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { CreateSkuTypeDto } from './dto/create-sku-type.dto';
import { UpdateSkuTypeDto } from './dto/update-sku-type.dto';
import { SkuTypesService } from './sku-types.service';

@ApiTags('sku-types')
@Controller('sku-types')
export class SkuTypesController {
  constructor(private readonly service: SkuTypesService) {}

  @Post()
  async create(@Body() dto: CreateSkuTypeDto) {
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
  async update(@Param('id') id: string, @Body() dto: UpdateSkuTypeDto) {
    return await this.service.update(id, dto);
  }
}
