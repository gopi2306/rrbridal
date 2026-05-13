import { Body, Controller, Get, Param, Patch, Post } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { CreateBranchDto } from './dto/create-branch.dto';
import { FilterBranchDto } from './dto/filter-branch.dto';
import { UpdateBranchDto } from './dto/update-branch.dto';
import { BranchesService } from './branches.service';

@ApiTags('branches')
@Controller('branches')
export class BranchesController {
  constructor(private readonly service: BranchesService) {}

  @Post()
  async create(@Body() dto: CreateBranchDto) {
    return await this.service.create(dto);
  }

  @Get()
  async list() {
    return await this.service.findAll();
  }

  @Post('filter')
  async filter(@Body() dto: FilterBranchDto) {
    return await this.service.filter(dto);
  }

  @Get(':id')
  async get(@Param('id') id: string) {
    return await this.service.findById(id);
  }

  @Patch(':id')
  async update(@Param('id') id: string, @Body() dto: UpdateBranchDto) {
    return await this.service.update(id, dto);
  }
}
