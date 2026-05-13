import { Body, Controller, Get, Param, Patch, Post } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { CreateBatchSelectionDto } from './dto/create-batch-selection.dto';
import { UpdateBatchSelectionDto } from './dto/update-batch-selection.dto';
import { BatchSelectionsService } from './batch-selections.service';

@ApiTags('batch-selections')
@Controller('batch-selections')
export class BatchSelectionsController {
  constructor(private readonly service: BatchSelectionsService) {}

  @Post()
  async create(@Body() dto: CreateBatchSelectionDto) {
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
  async update(@Param('id') id: string, @Body() dto: UpdateBatchSelectionDto) {
    return await this.service.update(id, dto);
  }
}
