import { Body, Controller, Get, Param, Patch, Post } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { CreatePackedConfirmationDto } from './dto/create-packed-confirmation.dto';
import { UpdatePackedConfirmationDto } from './dto/update-packed-confirmation.dto';
import { PackedConfirmationsService } from './packed-confirmations.service';

@ApiTags('packed-confirmations')
@Controller('packed-confirmations')
export class PackedConfirmationsController {
  constructor(private readonly service: PackedConfirmationsService) {}

  @Post()
  async create(@Body() dto: CreatePackedConfirmationDto) {
    return await this.service.create(dto);
  }

  @Get()
  async list() {
    return await this.service.findAll();
  }

  @Get(':id')
  async get(@Param('id') id: string) {
    return await this.service.findById(id);
  }

  @Patch(':id')
  async update(@Param('id') id: string, @Body() dto: UpdatePackedConfirmationDto) {
    return await this.service.update(id, dto);
  }
}
