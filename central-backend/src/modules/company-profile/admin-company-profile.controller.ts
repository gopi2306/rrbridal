import { Body, Controller, Get, Patch, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { CompanyProfileService } from './company-profile.service';
import { PatchCompanyProfileDto } from './dto/patch-company-profile.dto';

@ApiTags('admin-company-profile')
@ApiBearerAuth()
@Controller('admin/company-profile')
@UseGuards(JwtAuthGuard)
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
