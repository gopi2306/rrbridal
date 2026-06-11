import { Body, Controller, Get, Patch, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { AuthSettingsService } from './auth-settings.service';
import { PatchAuthSettingsDto } from './dto/patch-auth-settings.dto';

@ApiTags('admin-auth-settings')
@ApiBearerAuth()
@Controller('admin/auth-settings')
@UseGuards(JwtAuthGuard)
export class AdminAuthSettingsController {
  constructor(private readonly authSettingsService: AuthSettingsService) {}

  @Get()
  async get() {
    return await this.authSettingsService.getQuotas();
  }

  @Patch()
  async patch(@Body() dto: PatchAuthSettingsDto) {
    return await this.authSettingsService.updateRoleQuotas(dto.roleQuotas);
  }
}
