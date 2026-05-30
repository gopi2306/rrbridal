import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { Product, ProductSchema } from '../products/schemas/product.schema';
import { ProductsModule } from '../products/products.module';
import { StoresModule } from '../stores/stores.module';
import { InventoryExportController } from './inventory-export.controller';
import { InventoryExportService } from './inventory-export.service';
import { InventoryController } from './inventory.controller';
import { InventoryLedgerEntry, InventoryLedgerSchema } from './schemas/inventory-ledger.schema';
import { InventoryService } from './inventory.service';

@Module({
  imports: [
    MongooseModule.forFeature([
      { name: InventoryLedgerEntry.name, schema: InventoryLedgerSchema },
      { name: Product.name, schema: ProductSchema },
    ]),
    ProductsModule,
    StoresModule,
  ],
  controllers: [InventoryController, InventoryExportController],
  providers: [InventoryService, InventoryExportService],
  exports: [InventoryService],
})
export class InventoryModule {}

