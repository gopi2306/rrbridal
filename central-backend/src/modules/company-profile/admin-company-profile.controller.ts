import { Body, Controller, Get, Patch, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiTags } from '@nestjs/swagger';
import { Roles } from '../../common/decorators/roles.decorator';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { RolesGuard } from '../../common/guards/roles.guard';
import { CompanyProfileService } from './company-profile.service';
import { PatchCompanyProfileDto } from './dto/patch-company-profile.dto';

@ApiTags('admin-company-profile')
@ApiBearerAuth()
@Controller('admin/company-profile')
@UseGuards(JwtAuthGuard, RolesGuard)
@Roles('super_admin')
export class AdminCompanyProfileController {
  constructor(private readonly companyProfileService: CompanyProfileService) {}

  @Get()
  async get() {
    return await this.companyProfileService.get();
  }

  @Patch()
  async patch(@Body() dto: PatchCompanyProfileDto) {
    return await this.companyProfileService.patch(dto);
  }
}
