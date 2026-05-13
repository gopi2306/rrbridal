import { Body, Controller, Get, Param, Patch, Post } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { CreateWeightUnitDto } from './dto/create-weight-unit.dto';
import { UpdateWeightUnitDto } from './dto/update-weight-unit.dto';
import { WeightUnitsService } from './weight-units.service';

@ApiTags('weight-units')
@Controller('weight-units')
export class WeightUnitsController {
  constructor(private readonly service: WeightUnitsService) {}

  @Post()
  async create(@Body() dto: CreateWeightUnitDto) {
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
  async update(@Param('id') id: string, @Body() dto: UpdateWeightUnitDto) {
    return await this.service.update(id, dto);
  }
}
