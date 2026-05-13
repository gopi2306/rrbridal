import { Body, Controller, Get, Param, Patch, Post } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { CreateOfferGroupDto } from './dto/create-offer-group.dto';
import { UpdateOfferGroupDto } from './dto/update-offer-group.dto';
import { OfferGroupsService } from './offer-groups.service';

@ApiTags('offer-groups')
@Controller('offer-groups')
export class OfferGroupsController {
  constructor(private readonly service: OfferGroupsService) {}

  @Post()
  async create(@Body() dto: CreateOfferGroupDto) {
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
  async update(@Param('id') id: string, @Body() dto: UpdateOfferGroupDto) {
    return await this.service.update(id, dto);
  }
}
