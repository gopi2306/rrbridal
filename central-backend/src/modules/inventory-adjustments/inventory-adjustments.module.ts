import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { DocumentNumbersModule } from '../document-numbers/document-numbers.module';
import { InventoryModule } from '../inventory/inventory.module';
import { Location, LocationSchema } from '../locations/schemas/location.schema';
import { ProductsModule } from '../products/products.module';
import { Store, StoreSchema } from '../stores/schemas/store.schema';
import { InventoryAdjustmentsController } from './inventory-adjustments.controller';
import {
  InventoryAdjustment,
  InventoryAdjustmentSchema,
} from './schemas/inventory-adjustment.schema';
import { InventoryAdjustmentsService } from './inventory-adjustments.service';

@Module({
  imports: [
    DocumentNumbersModule,
    InventoryModule,
    ProductsModule,
    MongooseModule.forFeature([
      { name: InventoryAdjustment.name, schema: InventoryAdjustmentSchema },
      { name: Store.name, schema: StoreSchema },
      { name: Location.name, schema: LocationSchema },
    ]),
  ],
  controllers: [InventoryAdjustmentsController],
  providers: [InventoryAdjustmentsService],
  exports: [InventoryAdjustmentsService],
})
export class InventoryAdjustmentsModule {}
