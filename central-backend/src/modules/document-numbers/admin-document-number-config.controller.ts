import { Body, Controller, Get, Param, Patch, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiTags } from '@nestjs/swagger';
import { Roles } from '../../common/decorators/roles.decorator';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { RolesGuard } from '../../common/guards/roles.guard';
import { DocumentNumberConfigService } from './document-number-config.service';
import { PatchDocumentNumberConfigDto } from './dto/patch-document-number-config.dto';

@ApiTags('admin-document-number-configs')
@ApiBearerAuth()
@Controller('admin/document-number-configs')
@UseGuards(JwtAuthGuard, RolesGuard)
@Roles('admin', 'super_admin')
export class AdminDocumentNumberConfigController {
  constructor(private readonly configService: DocumentNumberConfigService) {}

  @Get()
  async list() {
    return await this.configService.listAll();
  }

  @Patch(':configKey')
  async patch(@Param('configKey') configKey: string, @Body() dto: PatchDocumentNumberConfigDto) {
    return await this.configService.patch(configKey, dto);
  }
}
