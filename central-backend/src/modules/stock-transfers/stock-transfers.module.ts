import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { InventoryModule } from '../inventory/inventory.module';
import { LocationsModule } from '../locations/locations.module';
import { PurchaseIntentsModule } from '../purchase-intents/purchase-intents.module';
import { StoresModule } from '../stores/stores.module';
import { StockTransfer, StockTransferSchema } from './schemas/stock-transfer.schema';
import { StockTransfersController } from './stock-transfers.controller';
import { StockTransfersService } from './stock-transfers.service';

@Module({
  imports: [
    MongooseModule.forFeature([{ name: StockTransfer.name, schema: StockTransferSchema }]),
    PurchaseIntentsModule,
    InventoryModule,
    StoresModule,
    LocationsModule,
  ],
  controllers: [StockTransfersController],
  providers: [StockTransfersService],
  exports: [StockTransfersService],
})
export class StockTransfersModule {}
