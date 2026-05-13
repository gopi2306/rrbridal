import { Body, Controller, Delete, Get, Param, Patch, Post, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiTags } from '@nestjs/swagger';
import { Roles } from '../../common/decorators/roles.decorator';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { RolesGuard } from '../../common/guards/roles.guard';
import { CreateStoreDto } from './dto/create-store.dto';
import { FilterStoreDto } from './dto/filter-store.dto';
import { UpdateStoreDto } from './dto/update-store.dto';
import { StoresService } from './stores.service';

@ApiTags('admin-stores')
@ApiBearerAuth()
@Controller('admin/stores')
@UseGuards(JwtAuthGuard, RolesGuard)
@Roles('admin')
export class AdminStoresController {
  constructor(private readonly storesService: StoresService) {}

  @Post()
  async create(@Body() dto: CreateStoreDto) {
    return await this.storesService.create(dto);
  }

  @Post('filter')
  async filter(@Body() dto: FilterStoreDto) {
    return await this.storesService.filter(dto);
  }

  @Get()
  async list() {
    return await this.storesService.findAll();
  }

  @Get(':code')
  async get(@Param('code') code: string) {
    return await this.storesService.findByCode(code);
  }

  @Patch(':code')
  async update(@Param('code') code: string, @Body() dto: UpdateStoreDto) {
    return await this.storesService.update(code, dto);
  }

  @Delete(':code')
  async remove(@Param('code') code: string) {
    return await this.storesService.removeByCode(code);
  }
}
