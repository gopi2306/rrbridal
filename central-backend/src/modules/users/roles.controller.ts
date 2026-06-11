import { Body, Controller, Delete, Get, Param, Patch, Post, Query, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiQuery, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { CreateRoleDefinitionDto } from './dto/create-role-definition.dto';
import { UpdateRoleDefinitionDto } from './dto/update-role-definition.dto';
import { RolesService } from './roles.service';

@ApiTags('roles')
@ApiBearerAuth()
@Controller('roles')
@UseGuards(JwtAuthGuard)
export class RolesController {
  constructor(private readonly rolesService: RolesService) {}

  @Post()
  async create(@Body() dto: CreateRoleDefinitionDto) {
    return await this.rolesService.create(dto);
  }

  @Get()
  @ApiQuery({
    name: 'includeInactive',
    required: false,
    description: 'Include roles with isActive=false (still excludes soft-deleted)',
  })
  async list(@Query('includeInactive') includeInactive?: string) {
    const include = includeInactive === 'true' || includeInactive === '1';
    return await this.rolesService.list(include);
  }

  @Get('code/:code')
  async getByCode(@Param('code') code: string) {
    return await this.rolesService.findByCode(code);
  }

  @Get(':id')
  async get(@Param('id') id: string) {
    return await this.rolesService.findById(id);
  }

  @Patch(':id')
  async update(@Param('id') id: string, @Body() dto: UpdateRoleDefinitionDto) {
    return await this.rolesService.update(id, dto);
  }

  @Delete(':id')
  async softDelete(@Param('id') id: string) {
    return await this.rolesService.softDelete(id);
  }
}
