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
  @ApiQuery({
    name: 'sinceCursor',
    required: false,
    description:
      'Product sync cursor. Prefer `{updatedAtIso}|{objectId}` from the previous pull. Pass 0 for first sync. A legacy bare product ObjectId triggers a one-time full product catch-up.',
  })
  @ApiQuery({
    name: 'sinceTransferCursor',
    required: false,
    description:
      'Last stock transfer _id from pull (completed transfers for multi-device); pass 0 to bootstrap last 90d window',
  })
  @ApiQuery({
    name: 'sincePromotionCursor',
    required: false,
    description: 'Last promotion scheme _id from pull; pass 0 for first sync',
  })
  @ApiQuery({
    name: 'sinceAdjustmentCursor',
    required: false,
    description: 'Last inventory adjustment _id from pull; pass 0 to bootstrap last 90d window',
  })
  @ApiQuery({ name: 'limit', required: false })
  async pull(
    @Query('storeId') storeId: string,
    @Query('sinceCursor') sinceCursor?: string,
    @Query('sinceTransferCursor') sinceTransferCursor?: string,
    @Query('sincePromotionCursor') sincePromotionCursor?: string,
    @Query('sinceAdjustmentCursor') sinceAdjustmentCursor?: string,
    @Query('limit') limit?: string,
  ) {
    const parsedLimit = limit ? Math.max(1, Math.min(1000, Number(limit))) : 200;
    return await this.syncService.pull(
      storeId,
      sinceCursor ?? '0',
      parsedLimit,
      sinceTransferCursor ?? '0',
      sincePromotionCursor ?? '0',
      sinceAdjustmentCursor ?? '0',
    );
  }

  @Get('health')
  health() {
    return { ok: true };
  }
}

