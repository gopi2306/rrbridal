import { Body, Controller, Get, Param, Patch, Post } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { CreateSellByTypeDto } from './dto/create-sell-by-type.dto';
import { UpdateSellByTypeDto } from './dto/update-sell-by-type.dto';
import { SellByTypesService } from './sell-by-types.service';

@ApiTags('sell-by-types')
@Controller('sell-by-types')
export class SellByTypesController {
  constructor(private readonly service: SellByTypesService) {}

  @Post()
  async create(@Body() dto: CreateSellByTypeDto) {
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
  async update(@Param('id') id: string, @Body() dto: UpdateSellByTypeDto) {
    return await this.service.update(id, dto);
  }
}
