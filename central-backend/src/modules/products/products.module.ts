import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { ProductSkuGenerator } from './product-sku.generator';
import { IdSequence, IdSequenceSchema } from './schemas/id-sequence.schema';
import { Product, ProductSchema } from './schemas/product.schema';
import { ProductsController } from './products.controller';
import { ProductsService } from './products.service';

@Module({
  imports: [
    MongooseModule.forFeature([
      { name: Product.name, schema: ProductSchema },
      { name: IdSequence.name, schema: IdSequenceSchema },
    ]),
  ],
  controllers: [ProductsController],
  providers: [ProductsService, ProductSkuGenerator],
  exports: [ProductsService, ProductSkuGenerator],
})
export class ProductsModule {}

