import { Body, Controller, Get, Param, Patch, Post, Query } from '@nestjs/common';
import { ApiQuery, ApiTags } from '@nestjs/swagger';
import { CreateSalesmanDto } from './dto/create-salesman.dto';
import { UpdateSalesmanDto } from './dto/update-salesman.dto';
import { SalesmenService } from './salesmen.service';

@ApiTags('salesmen')
@Controller('salesmen')
export class SalesmenController {
  constructor(private readonly salesmenService: SalesmenService) {}

  @Get()
  @ApiQuery({ name: 'storeId', required: true })
  @ApiQuery({ name: 'search', required: false })
  async list(@Query('storeId') storeId: string, @Query('search') search?: string) {
    return await this.salesmenService.listByStore(storeId, search);
  }

  @Post()
  async create(@Body() dto: CreateSalesmanDto) {
    return await this.salesmenService.create(dto);
  }

  @Get(':id')
  async get(@Param('id') id: string) {
    return await this.salesmenService.findById(id);
  }

  @Patch(':id')
  async update(@Param('id') id: string, @Body() dto: UpdateSalesmanDto) {
    return await this.salesmenService.update(id, dto);
  }
}
