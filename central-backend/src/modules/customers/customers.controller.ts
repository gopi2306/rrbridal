import { Body, Controller, Get, Param, Patch, Post, Query } from '@nestjs/common';
import { ApiQuery, ApiTags } from '@nestjs/swagger';
import { CreateCustomerDto } from './dto/create-customer.dto';
import { UpdateCustomerDto } from './dto/update-customer.dto';
import { CustomersService } from './customers.service';

@ApiTags('customers')
@Controller('customers')
export class CustomersController {
  constructor(private readonly customersService: CustomersService) {}

  @Post()
  async create(@Body() dto: CreateCustomerDto) {
    return await this.customersService.create(dto);
  }

  @Get(':id')
  async get(@Param('id') id: string) {
    return await this.customersService.findById(id);
  }

  @Patch(':id')
  async update(@Param('id') id: string, @Body() dto: UpdateCustomerDto) {
    return await this.customersService.update(id, dto);
  }

  @Get()
  @ApiQuery({ name: 'search', required: false })
  @ApiQuery({ name: 'phone', required: false })
  async list(@Query('search') search?: string, @Query('phone') phone?: string) {
    const params: { search?: string; phone?: string } = {};
    if (search) params.search = search;
    if (phone) params.phone = phone;
    return await this.customersService.list(params);
  }
}

