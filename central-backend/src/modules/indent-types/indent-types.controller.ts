import { Body, Controller, Get, Param, Patch, Post } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { CreateIndentTypeDto } from './dto/create-indent-type.dto';
import { UpdateIndentTypeDto } from './dto/update-indent-type.dto';
import { IndentTypesService } from './indent-types.service';

@ApiTags('indent-types')
@Controller('indent-types')
export class IndentTypesController {
  constructor(private readonly service: IndentTypesService) {}

  @Post()
  async create(@Body() dto: CreateIndentTypeDto) {
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
  async update(@Param('id') id: string, @Body() dto: UpdateIndentTypeDto) {
    return await this.service.update(id, dto);
  }
}
