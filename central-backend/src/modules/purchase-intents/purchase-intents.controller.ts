import { Body, Controller, Get, Param, Patch, Post, Query } from '@nestjs/common';
import { ApiQuery, ApiTags } from '@nestjs/swagger';
import { CreatePurchaseIntentDto } from './dto/create-purchase-intent.dto';
import { SetPurchaseIntentStatusDto } from './dto/set-purchase-intent-status.dto';
import { UpdatePurchaseIntentDto } from './dto/update-purchase-intent.dto';
import { PurchaseIntentsService } from './purchase-intents.service';

@ApiTags('purchase-intents')
@Controller('purchase-intents')
export class PurchaseIntentsController {
  constructor(private readonly intentsService: PurchaseIntentsService) {}

  @Post()
  async create(@Body() dto: CreatePurchaseIntentDto) {
    return await this.intentsService.create(dto);
  }

  @Get()
  @ApiQuery({ name: 'search', required: false, description: 'Search by intent number' })
  @ApiQuery({ name: 'storeId', required: false })
  @ApiQuery({ name: 'status', required: false })
  async list(@Query('search') search?: string, @Query('storeId') storeId?: string, @Query('status') status?: string) {
    const params: { search?: string; storeId?: string; status?: string } = {};
    if (search) params.search = search;
    if (storeId) params.storeId = storeId;
    if (status) params.status = status;
    return await this.intentsService.list(params);
  }

  @Get(':id')
  async get(@Param('id') id: string) {
    return await this.intentsService.findById(id);
  }

  @Patch(':id')
  async update(@Param('id') id: string, @Body() dto: UpdatePurchaseIntentDto) {
    return await this.intentsService.update(id, dto);
  }

  @Post(':id/status')
  async setStatus(@Param('id') id: string, @Body() dto: SetPurchaseIntentStatusDto) {
    return await this.intentsService.setStatus(id, dto.status);
  }
}
