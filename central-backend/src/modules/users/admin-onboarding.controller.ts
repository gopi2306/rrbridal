import { Body, Controller, Get, Patch, Post, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { InitialAdminDto } from './dto/initial-admin.dto';
import { UpdateInitialAdminDto } from './dto/update-initial-admin.dto';
import { UsersService } from './users.service';

@ApiTags('admin-onboarding')
@ApiBearerAuth()
@Controller('admin/onboarding')
@UseGuards(JwtAuthGuard)
export class AdminOnboardingController {
  constructor(private readonly usersService: UsersService) {}

  @Get('initial-admin')
  async getInitialAdmin() {
    return await this.usersService.getInitialAdmin();
  }

  @Post('initial-admin')
  async createInitialAdmin(@Body() dto: InitialAdminDto) {
    return await this.usersService.createInitialAdmin(dto);
  }

  @Patch('initial-admin')
  async updateInitialAdmin(@Body() dto: UpdateInitialAdminDto) {
    return await this.usersService.updateInitialAdmin(dto);
  }
}
