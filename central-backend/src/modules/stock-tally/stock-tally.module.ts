import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { Product, ProductSchema } from '../products/schemas/product.schema';
import { ProductsModule } from '../products/products.module';
import { StockAuditModule } from '../stock-audit/stock-audit.module';
import { Store, StoreSchema } from '../stores/schemas/store.schema';
import { StockTally, StockTallySchema } from './schemas/stock-tally.schema';
import { StockTallyController } from './stock-tally.controller';
import { StockTallyService } from './stock-tally.service';

@Module({
  imports: [
    ProductsModule,
    StockAuditModule,
    MongooseModule.forFeature([
      { name: StockTally.name, schema: StockTallySchema },
      { name: Store.name, schema: StoreSchema },
      { name: Product.name, schema: ProductSchema },
    ]),
  ],
  controllers: [StockTallyController],
  providers: [StockTallyService],
})
export class StockTallyModule {}
