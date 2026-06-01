import { Body, Controller, Delete, Get, Param, Patch, Post } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { CreatePromotionSchemeDto } from './dto/create-promotion-scheme.dto';
import { FilterPromotionSchemeDto } from './dto/filter-promotion-scheme.dto';
import { UpdatePromotionSchemeDto } from './dto/update-promotion-scheme.dto';
import { PromotionSchemesService } from './promotion-schemes.service';

@ApiTags('promotion-schemes')
@Controller('promotion-schemes')
export class PromotionSchemesController {
  constructor(private readonly service: PromotionSchemesService) {}

  @Post()
  async create(@Body() dto: CreatePromotionSchemeDto) {
    return await this.service.create(dto);
  }

  @Post('filter')
  async filter(@Body() dto: FilterPromotionSchemeDto) {
    return await this.service.filter(dto);
  }

  @Get()
  async list() {
    return await this.service.findAll();
  }

  @Get('code/:code')
  async getByCode(@Param('code') code: string) {
    return await this.service.findByCode(code);
  }

  @Get(':id')
  async get(@Param('id') id: string) {
    return await this.service.findById(id);
  }

  @Patch(':id')
  async update(@Param('id') id: string, @Body() dto: UpdatePromotionSchemeDto) {
    return await this.service.update(id, dto);
  }

  @Patch(':id/deactivate')
  async deactivate(@Param('id') id: string) {
    return await this.service.deactivate(id);
  }

  @Delete(':id')
  async remove(@Param('id') id: string) {
    return await this.service.softDelete(id);
  }
}
