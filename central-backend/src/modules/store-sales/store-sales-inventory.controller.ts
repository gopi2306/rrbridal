import { Controller, Post, Query } from '@nestjs/common';
import { ApiQuery, ApiTags } from '@nestjs/swagger';
import { StoreSalesInventoryService } from './store-sales-inventory.service';

@ApiTags('store-sales')
@Controller('store-sales/inventory')
export class StoreSalesInventoryController {
  constructor(private readonly storeSalesInventoryService: StoreSalesInventoryService) {}

  @Post('backfill')
  @ApiQuery({ name: 'dryRun', required: false, type: Boolean })
  @ApiQuery({ name: 'batchSize', required: false, type: Number })
  async backfill(
    @Query('dryRun') dryRun?: string,
    @Query('batchSize') batchSize?: string,
  ) {
    const parsedBatch = batchSize ? Number(batchSize) : NaN;
    const options: { dryRun?: boolean; batchSize?: number } = {
      dryRun: dryRun === 'true' || dryRun === '1',
    };
    if (Number.isFinite(parsedBatch)) options.batchSize = parsedBatch;
    return await this.storeSalesInventoryService.backfillAll(options);
  }
}
