import { Body, Controller, Get, Param, Patch, Post } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { CreateBatchExpiryDetailDto } from './dto/create-batch-expiry-detail.dto';
import { UpdateBatchExpiryDetailDto } from './dto/update-batch-expiry-detail.dto';
import { BatchExpiryDetailsService } from './batch-expiry-details.service';

@ApiTags('batch-expiry-details')
@Controller('batch-expiry-details')
export class BatchExpiryDetailsController {
  constructor(private readonly service: BatchExpiryDetailsService) {}

  @Post()
  async create(@Body() dto: CreateBatchExpiryDetailDto) {
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
  async update(@Param('id') id: string, @Body() dto: UpdateBatchExpiryDetailDto) {
    return await this.service.update(id, dto);
  }
}
