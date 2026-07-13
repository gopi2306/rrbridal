import { Controller, Get, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiOkResponse, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { BarcodeLabelDesignService } from './barcode-label-design.service';

@ApiTags('barcode-label-designs')
@ApiBearerAuth()
@Controller('barcode-label-designs')
@UseGuards(JwtAuthGuard)
export class BarcodeLabelDesignController {
  constructor(private readonly designService: BarcodeLabelDesignService) {}

  @Get('active')
  @ApiOkResponse({ description: 'Active company-wide barcode label design for store sync' })
  async getActive() {
    return await this.designService.getActiveDesign();
  }

  @Get('printer-profiles')
  async listPrinterProfiles() {
    return await this.designService.listPrinterProfiles();
  }
}
