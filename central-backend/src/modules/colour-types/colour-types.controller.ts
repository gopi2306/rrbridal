import { Body, Controller, Get, Param, Patch, Post } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { CreateColourTypeDto } from './dto/create-colour-type.dto';
import { UpdateColourTypeDto } from './dto/update-colour-type.dto';
import { ColourTypesService } from './colour-types.service';

@ApiTags('colour-types')
@Controller('colour-types')
export class ColourTypesController {
  constructor(private readonly service: ColourTypesService) {}

  @Post()
  async create(@Body() dto: CreateColourTypeDto) {
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
  async update(@Param('id') id: string, @Body() dto: UpdateColourTypeDto) {
    return await this.service.update(id, dto);
  }
}
