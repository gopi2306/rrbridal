import { Body, Controller, Get, Param, Patch, Post } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { CreateBrandDto } from './dto/create-brand.dto';
import { FilterBrandDto } from './dto/filter-brand.dto';
import { UpdateBrandDto } from './dto/update-brand.dto';
import { BrandsService } from './brands.service';

@ApiTags('brands')
@Controller('brands')
export class BrandsController {
  constructor(private readonly service: BrandsService) {}

  @Post()
  async create(@Body() dto: CreateBrandDto) {
    return await this.service.create(dto);
  }

  @Get()
  async list() {
    return await this.service.findAll();
  }

  @Post('filter')
  async filter(@Body() dto: FilterBrandDto) {
    return await this.service.filter(dto);
  }

  @Get(':id')
  async get(@Param('id') id: string) {
    return await this.service.findById(id);
  }

  @Patch(':id')
  async update(@Param('id') id: string, @Body() dto: UpdateBrandDto) {
    return await this.service.update(id, dto);
  }
}
