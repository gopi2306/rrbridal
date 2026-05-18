import { Controller, Get, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { CompanyProfileService } from './company-profile.service';

@ApiTags('company-profile')
@ApiBearerAuth()
@Controller('company-profile')
@UseGuards(JwtAuthGuard)
export class CompanyProfileController {
  constructor(private readonly companyProfileService: CompanyProfileService) {}

  @Get()
  async get() {
    return await this.companyProfileService.get();
  }
}
