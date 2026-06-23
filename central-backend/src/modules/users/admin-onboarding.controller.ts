import { BadRequestException, Body, Controller, Get, Patch, Post, UseGuards } from '@nestjs/common';
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
    const { exists } = await this.usersService.getInitialAdmin();
    if (!exists) {
      const name = dto.name?.trim();
      const email = dto.email?.trim();
      const password = dto.password;
      if (!name || !email || !password) {
        throw new BadRequestException(
          'Operational admin not found. Provide name, email, and password to create the initial admin.',
        );
      }
      return await this.usersService.createInitialAdmin({ name, email, password });
    }
    return await this.usersService.updateInitialAdmin(dto);
  }
}
