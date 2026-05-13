import { Body, Controller, Get, Param, Patch, Post } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { CreateHsnCodeDto } from './dto/create-hsn-code.dto';
import { UpdateHsnCodeDto } from './dto/update-hsn-code.dto';
import { HsnCodesService } from './hsn-codes.service';

@ApiTags('hsn-codes')
@Controller('hsn-codes')
export class HsnCodesController {
  constructor(private readonly service: HsnCodesService) {}

  @Post()
  async create(@Body() dto: CreateHsnCodeDto) {
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
  async update(@Param('id') id: string, @Body() dto: UpdateHsnCodeDto) {
    return await this.service.update(id, dto);
  }
}
