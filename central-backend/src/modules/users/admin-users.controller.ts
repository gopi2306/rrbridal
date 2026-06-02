import { Body, Controller, Get, Param, Patch, Post, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiTags } from '@nestjs/swagger';
import { CurrentUser } from '../../common/decorators/current-user.decorator';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import type { JwtPayload } from '../../common/jwt-payload';
import { CreateUserDto } from './dto/create-user.dto';
import { FilterUserDto } from './dto/filter-user.dto';
import { UpdateUserDto } from './dto/update-user.dto';
import { UsersService } from './users.service';

@ApiTags('admin-users')
@ApiBearerAuth()
@Controller('admin/users')
@UseGuards(JwtAuthGuard)
export class AdminUsersController {
  constructor(private readonly usersService: UsersService) {}

  @Post()
  async create(@Body() dto: CreateUserDto) {
    return await this.usersService.create(dto);
  }

  @Post('filter')
  async filter(@Body() dto: FilterUserDto) {
    return await this.usersService.filter(dto);
  }

  @Get()
  async list() {
    return await this.usersService.list();
  }

  @Get('me')
  async me(@CurrentUser() user: JwtPayload) {
    return await this.usersService.findById(user.sub);
  }

  @Get(':id')
  async get(@Param('id') id: string) {
    return await this.usersService.findById(id);
  }

  @Patch(':id')
  async update(@Param('id') id: string, @Body() dto: UpdateUserDto) {
    return await this.usersService.update(id, dto);
  }

  @Post(':id/disable')
  async disable(@Param('id') id: string) {
    return await this.usersService.disable(id);
  }
}
