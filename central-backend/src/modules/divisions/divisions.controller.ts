import { Body, Controller, Get, Param, Patch, Post } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { CreateDivisionDto } from './dto/create-division.dto';
import { UpdateDivisionDto } from './dto/update-division.dto';
import { DivisionsService } from './divisions.service';

@ApiTags('divisions')
@Controller('divisions')
export class DivisionsController {
  constructor(private readonly service: DivisionsService) {}

  @Post()
  async create(@Body() dto: CreateDivisionDto) {
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
  async update(@Param('id') id: string, @Body() dto: UpdateDivisionDto) {
    return await this.service.update(id, dto);
  }
}
