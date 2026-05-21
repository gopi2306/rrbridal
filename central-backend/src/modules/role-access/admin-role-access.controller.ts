import { Body, Controller, Delete, Get, Param, Patch, Post, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiTags } from '@nestjs/swagger';
import { Roles } from '../../common/decorators/roles.decorator';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { RolesGuard } from '../../common/guards/roles.guard';
import { CreateRoleAccessDto } from './dto/create-role-access.dto';
import { FilterRoleAccessDto } from './dto/filter-role-access.dto';
import { UpdateRoleAccessDto } from './dto/update-role-access.dto';
import { UpsertRoleAccessByRoleDto } from './dto/upsert-role-access-by-role.dto';
import { RoleAccessService } from './role-access.service';

@ApiTags('admin-role-access')
@ApiBearerAuth()
@Controller('admin/role-access')
@UseGuards(JwtAuthGuard, RolesGuard)
@Roles('admin', 'super_admin')
export class AdminRoleAccessController {
  constructor(private readonly service: RoleAccessService) {}

  @Post()
  async create(@Body() dto: CreateRoleAccessDto) {
    return await this.service.create(dto);
  }

  @Post('filter')
  async filter(@Body() dto: FilterRoleAccessDto) {
    return await this.service.filter(dto);
  }

  @Get('by-role/:role')
  async listForRole(@Param('role') role: string) {
    return await this.service.listForRole(role);
  }

  @Patch('by-role/:role')
  async upsertByRole(@Param('role') role: string, @Body() dto: UpsertRoleAccessByRoleDto) {
    return await this.service.upsertByRole(role, dto.permissions);
  }

  @Post('by-role/:role/allow-all')
  async allowAll(@Param('role') role: string) {
    return await this.service.allowAllScreensForRole(role);
  }

  @Get(':id')
  async get(@Param('id') id: string) {
    return await this.service.findById(id);
  }

  @Patch(':id')
  async update(@Param('id') id: string, @Body() dto: UpdateRoleAccessDto) {
    return await this.service.update(id, dto);
  }

  @Delete(':id')
  async remove(@Param('id') id: string) {
    return await this.service.remove(id);
  }
}
