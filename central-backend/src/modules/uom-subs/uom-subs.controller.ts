import { Body, Controller, Get, Param, Patch, Post } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { CreateUomSubDto } from './dto/create-uom-sub.dto';
import { UpdateUomSubDto } from './dto/update-uom-sub.dto';
import { UomSubsService } from './uom-subs.service';

@ApiTags('uom-subs')
@Controller('uom-subs')
export class UomSubsController {
  constructor(private readonly service: UomSubsService) {}

  @Post()
  async create(@Body() dto: CreateUomSubDto) {
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
  async update(@Param('id') id: string, @Body() dto: UpdateUomSubDto) {
    return await this.service.update(id, dto);
  }
}
