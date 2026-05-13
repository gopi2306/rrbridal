import { Body, Controller, Get, Param, Patch, Post, Query } from '@nestjs/common';
import { ApiQuery, ApiTags } from '@nestjs/swagger';
import { CreateSupplierDto } from './dto/create-supplier.dto';
import { UpdateSupplierDto } from './dto/update-supplier.dto';
import { SuppliersService } from './suppliers.service';

@ApiTags('suppliers')
@Controller('suppliers')
export class SuppliersController {
  constructor(private readonly suppliersService: SuppliersService) {}

  @Post()
  async create(@Body() dto: CreateSupplierDto) {
    return await this.suppliersService.create(dto);
  }

  @Get(':id')
  async get(@Param('id') id: string) {
    return await this.suppliersService.findById(id);
  }

  @Patch(':id')
  async update(@Param('id') id: string, @Body() dto: UpdateSupplierDto) {
    return await this.suppliersService.update(id, dto);
  }

  @Get()
  @ApiQuery({ name: 'search', required: false })
  @ApiQuery({ name: 'mobileNo', required: false })
  async list(@Query('search') search?: string, @Query('mobileNo') mobileNo?: string) {
    const params: { search?: string; mobileNo?: string } = {};
    if (search) params.search = search;
    if (mobileNo) params.mobileNo = mobileNo;
    return await this.suppliersService.list(params);
  }
}
