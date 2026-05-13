import { Body, Controller, Get, Patch, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiTags } from '@nestjs/swagger';
import { Roles } from '../../common/decorators/roles.decorator';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { RolesGuard } from '../../common/guards/roles.guard';
import { PatchResourceLimitsDto } from './dto/patch-resource-limits.dto';
import { ResourceLimitsService } from './resource-limits.service';

@ApiTags('admin-resource-limits')
@ApiBearerAuth()
@Controller('admin/resource-limits')
@UseGuards(JwtAuthGuard, RolesGuard)
@Roles('admin')
export class ResourceLimitsController {
  constructor(private readonly resourceLimitsService: ResourceLimitsService) {}

  @Get()
  async get() {
    return await this.resourceLimitsService.getUsage();
  }

  @Patch()
  async patch(@Body() dto: PatchResourceLimitsDto) {
    return await this.resourceLimitsService.patch(dto);
  }
}
