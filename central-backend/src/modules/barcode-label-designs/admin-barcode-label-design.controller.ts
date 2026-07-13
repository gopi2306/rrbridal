import { Body, Controller, Delete, Get, Param, Patch, Post, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { BarcodeLabelDesignService } from './barcode-label-design.service';
import { CreateBarcodeLabelDesignDto } from './dto/create-barcode-label-design.dto';
import { UpdateBarcodeLabelDesignDto } from './dto/update-barcode-label-design.dto';

@ApiTags('admin-barcode-label-designs')
@ApiBearerAuth()
@Controller('admin/barcode-label-designs')
@UseGuards(JwtAuthGuard)
export class AdminBarcodeLabelDesignController {
  constructor(private readonly designService: BarcodeLabelDesignService) {}

  @Get()
  async list() {
    return await this.designService.listDesigns();
  }

  @Get('printer-profiles')
  async listPrinterProfiles() {
    return await this.designService.listPrinterProfiles();
  }

  @Post()
  async create(@Body() dto: CreateBarcodeLabelDesignDto) {
    return await this.designService.create(dto);
  }

  @Patch(':id')
  async update(@Param('id') id: string, @Body() dto: UpdateBarcodeLabelDesignDto) {
    return await this.designService.update(id, dto);
  }

  @Delete(':id')
  async remove(@Param('id') id: string) {
    return await this.designService.remove(id);
  }

  @Post(':id/activate')
  async activate(@Param('id') id: string) {
    return await this.designService.activate(id);
  }
}
