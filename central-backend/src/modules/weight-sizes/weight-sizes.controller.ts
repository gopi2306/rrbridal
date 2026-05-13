import { Body, Controller, Get, Param, Patch, Post } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { CreateWeightSizeDto } from './dto/create-weight-size.dto';
import { UpdateWeightSizeDto } from './dto/update-weight-size.dto';
import { WeightSizesService } from './weight-sizes.service';

@ApiTags('weight-sizes')
@Controller('weight-sizes')
export class WeightSizesController {
  constructor(private readonly service: WeightSizesService) {}

  @Post()
  async create(@Body() dto: CreateWeightSizeDto) {
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
  async update(@Param('id') id: string, @Body() dto: UpdateWeightSizeDto) {
    return await this.service.update(id, dto);
  }
}
