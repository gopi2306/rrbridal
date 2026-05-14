import { Body, Controller, Delete, Get, Param, Patch, Post, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiTags } from '@nestjs/swagger';
import { Roles } from '../../common/decorators/roles.decorator';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { RolesGuard } from '../../common/guards/roles.guard';
import { CreateLocationDto } from './dto/create-location.dto';
import { FilterLocationDto } from './dto/filter-location.dto';
import { UpdateLocationDto } from './dto/update-location.dto';
import { LocationsService } from './locations.service';

@ApiTags('locations')
@ApiBearerAuth()
@Controller('locations')
@UseGuards(JwtAuthGuard, RolesGuard)
@Roles('admin')
export class LocationsController {
  constructor(private readonly service: LocationsService) {}

  @Post()
  async create(@Body() dto: CreateLocationDto) {
    return await this.service.create(dto);
  }

  @Get()
  async list() {
    return await this.service.findAll();
  }

  @Post('filter')
  async filter(@Body() dto: FilterLocationDto) {
    return await this.service.filter(dto);
  }

  @Get(':id')
  async get(@Param('id') id: string) {
    return await this.service.findById(id);
  }

  @Patch(':id')
  async update(@Param('id') id: string, @Body() dto: UpdateLocationDto) {
    return await this.service.update(id, dto);
  }

  @Delete(':id')
  async remove(@Param('id') id: string) {
    return await this.service.remove(id);
  }
}
