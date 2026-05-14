import { Body, Controller, Post, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiTags } from '@nestjs/swagger';
import { Roles } from '../../common/decorators/roles.decorator';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { RolesGuard } from '../../common/guards/roles.guard';
import { InitialAdminDto } from './dto/initial-admin.dto';
import { UsersService } from './users.service';

@ApiTags('admin-onboarding')
@ApiBearerAuth()
@Controller('admin/onboarding')
@UseGuards(JwtAuthGuard, RolesGuard)
@Roles('super_admin')
export class AdminOnboardingController {
  constructor(private readonly usersService: UsersService) {}

  @Post('initial-admin')
  async createInitialAdmin(@Body() dto: InitialAdminDto) {
    return await this.usersService.createInitialAdmin(dto);
  }
}
