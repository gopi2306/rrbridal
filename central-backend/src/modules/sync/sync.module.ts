import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { AuthModule } from '../auth/auth.module';
import { ProductsModule } from '../products/products.module';
import { PurchaseIntentsModule } from '../purchase-intents/purchase-intents.module';
import { StoresModule } from '../stores/stores.module';
import { StockTransfersModule } from '../stock-transfers/stock-transfers.module';
import { StoreSalesModule } from '../store-sales/store-sales.module';
import { PromotionSchemesModule } from '../promotion-schemes/promotion-schemes.module';
import { InventoryAdjustmentsModule } from '../inventory-adjustments/inventory-adjustments.module';
import { SyncController } from './sync.controller';
import { SyncEvent, SyncEventSchema } from './schemas/sync-event.schema';
import { SyncCursor, SyncCursorSchema } from './schemas/sync-cursor.schema';
import { SyncService } from './sync.service';

@Module({
  imports: [
    AuthModule,
    ProductsModule,
    PurchaseIntentsModule,
    StoresModule,
    StockTransfersModule,
    StoreSalesModule,
    PromotionSchemesModule,
    InventoryAdjustmentsModule,
    MongooseModule.forFeature([
      { name: SyncEvent.name, schema: SyncEventSchema },
      { name: SyncCursor.name, schema: SyncCursorSchema },
    ]),
  ],
  controllers: [SyncController],
  providers: [SyncService],
})
export class SyncModule {}

