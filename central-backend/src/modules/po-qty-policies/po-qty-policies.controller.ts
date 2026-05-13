import { Body, Controller, Get, Param, Patch, Post } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { CreatePoQtyPolicyDto } from './dto/create-po-qty-policy.dto';
import { UpdatePoQtyPolicyDto } from './dto/update-po-qty-policy.dto';
import { PoQtyPoliciesService } from './po-qty-policies.service';

@ApiTags('po-qty-policies')
@Controller('po-qty-policies')
export class PoQtyPoliciesController {
  constructor(private readonly service: PoQtyPoliciesService) {}

  @Post()
  async create(@Body() dto: CreatePoQtyPolicyDto) {
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
  async update(@Param('id') id: string, @Body() dto: UpdatePoQtyPolicyDto) {
    return await this.service.update(id, dto);
  }
}
