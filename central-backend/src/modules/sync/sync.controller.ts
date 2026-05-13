import { Body, Controller, Get, Post, Query } from '@nestjs/common';
import { ApiQuery, ApiTags } from '@nestjs/swagger';
import { SyncPushDto } from './dto/sync-push.dto';
import { SyncService } from './sync.service';

@ApiTags('sync')
@Controller('sync')
export class SyncController {
  constructor(private readonly syncService: SyncService) {}

  @Post('push')
  async push(@Body() dto: SyncPushDto) {
    const results = await this.syncService.push(dto.events);
    return { results };
  }

  @Get('pull')
  @ApiQuery({ name: 'storeId', required: true })
  @ApiQuery({ name: 'sinceCursor', required: false, description: 'Last cursor received; pass 0 for first sync' })
  @ApiQuery({ name: 'limit', required: false })
  async pull(
    @Query('storeId') storeId: string,
    @Query('sinceCursor') sinceCursor?: string,
    @Query('limit') limit?: string,
  ) {
    const parsedLimit = limit ? Math.max(1, Math.min(1000, Number(limit))) : 200;
    return await this.syncService.pull(storeId, sinceCursor ?? '0', parsedLimit);
  }

  @Get('health')
  health() {
    return { ok: true };
  }
}

