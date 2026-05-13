import { Module } from '@nestjs/common';
import { GoodsReceiptsModule } from '../goods-receipts/goods-receipts.module';
import { StockTransfersModule } from '../stock-transfers/stock-transfers.module';
import { TestDataSeedService } from './test-data-seed.service';

@Module({
  imports: [GoodsReceiptsModule, StockTransfersModule],
  providers: [TestDataSeedService],
})
export class SeedModule {}
