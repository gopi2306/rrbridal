import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { InventoryModule } from '../inventory/inventory.module';
import { Product, ProductSchema } from '../products/schemas/product.schema';
import { ProductsModule } from '../products/products.module';
import { Store, StoreSchema } from '../stores/schemas/store.schema';
import { StockAudit, StockAuditSchema } from './schemas/stock-audit.schema';
import { StockAuditController } from './stock-audit.controller';
import { StockAuditExportService } from './stock-audit-export.service';
import { StockAuditService } from './stock-audit.service';

@Module({
  imports: [
    InventoryModule,
    ProductsModule,
    MongooseModule.forFeature([
      { name: StockAudit.name, schema: StockAuditSchema },
      { name: Store.name, schema: StoreSchema },
      { name: Product.name, schema: ProductSchema },
    ]),
  ],
  controllers: [StockAuditController],
  providers: [StockAuditService, StockAuditExportService],
  exports: [StockAuditService],
})
export class StockAuditModule {}
