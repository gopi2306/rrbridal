import { Module } from '@nestjs/common';
import { GoodsReceiptsModule } from '../goods-receipts/goods-receipts.module';
import { PromotionSchemesModule } from '../promotion-schemes/promotion-schemes.module';
import { StockTransfersModule } from '../stock-transfers/stock-transfers.module';
import { TestDataSeedService } from './test-data-seed.service';

@Module({
  imports: [GoodsReceiptsModule, StockTransfersModule, PromotionSchemesModule],
  providers: [TestDataSeedService],
})
export class SeedModule {}
