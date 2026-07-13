import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { Product, ProductSchema } from '../products/schemas/product.schema';
import { ProductsModule } from '../products/products.module';
import { Store, StoreSchema } from '../stores/schemas/store.schema';
import { StoresModule } from '../stores/stores.module';
import { Supplier, SupplierSchema } from '../suppliers/schemas/supplier.schema';
import { InventoryExportController } from './inventory-export.controller';
import { InventoryExportService } from './inventory-export.service';
import { InventoryController } from './inventory.controller';
import { Location, LocationSchema } from '../locations/schemas/location.schema';
import { InventoryLedgerEntry, InventoryLedgerSchema } from './schemas/inventory-ledger.schema';
import { InventoryService } from './inventory.service';
import { SupplierWiseReportController } from './supplier-wise-report.controller';
import { SupplierWiseReportExportService } from './supplier-wise-report-export.service';
import { SupplierWiseReportService } from './supplier-wise-report.service';

@Module({
  imports: [
    MongooseModule.forFeature([
      { name: InventoryLedgerEntry.name, schema: InventoryLedgerSchema },
      { name: Product.name, schema: ProductSchema },
      { name: Location.name, schema: LocationSchema },
      { name: Store.name, schema: StoreSchema },
      { name: Supplier.name, schema: SupplierSchema },
    ]),
    ProductsModule,
    StoresModule,
  ],
  controllers: [InventoryController, InventoryExportController, SupplierWiseReportController],
  providers: [
    InventoryService,
    InventoryExportService,
    SupplierWiseReportService,
    SupplierWiseReportExportService,
  ],
  exports: [InventoryService],
})
export class InventoryModule {}

