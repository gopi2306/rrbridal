import { Body, Controller, Get, Param, Patch, Post } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { CreateColourDto } from './dto/create-colour.dto';
import { UpdateColourDto } from './dto/update-colour.dto';
import { ColoursService } from './colours.service';

@ApiTags('colours')
@Controller('colours')
export class ColoursController {
  constructor(private readonly service: ColoursService) {}

  @Post()
  async create(@Body() dto: CreateColourDto) {
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
  async update(@Param('id') id: string, @Body() dto: UpdateColourDto) {
    return await this.service.update(id, dto);
  }
}
