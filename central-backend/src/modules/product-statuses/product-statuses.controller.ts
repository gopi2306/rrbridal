import { Body, Controller, Get, Param, Patch, Post } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { CreateProductStatusDto } from './dto/create-product-status.dto';
import { UpdateProductStatusDto } from './dto/update-product-status.dto';
import { ProductStatusesService } from './product-statuses.service';

@ApiTags('product-statuses')
@Controller('product-statuses')
export class ProductStatusesController {
  constructor(private readonly service: ProductStatusesService) {}

  @Post()
  async create(@Body() dto: CreateProductStatusDto) {
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
  async update(@Param('id') id: string, @Body() dto: UpdateProductStatusDto) {
    return await this.service.update(id, dto);
  }
}
