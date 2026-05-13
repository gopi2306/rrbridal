import { Body, Controller, Get, Param, Patch, Post } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { CreateGstUomDto } from './dto/create-gst-uom.dto';
import { UpdateGstUomDto } from './dto/update-gst-uom.dto';
import { GstUomsService } from './gst-uoms.service';

@ApiTags('gst-uoms')
@Controller('gst-uoms')
export class GstUomsController {
  constructor(private readonly service: GstUomsService) {}

  @Post()
  async create(@Body() dto: CreateGstUomDto) {
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
  async update(@Param('id') id: string, @Body() dto: UpdateGstUomDto) {
    return await this.service.update(id, dto);
  }
}
