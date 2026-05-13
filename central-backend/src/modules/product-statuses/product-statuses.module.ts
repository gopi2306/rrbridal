import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { ProductStatus, ProductStatusSchema } from './schemas/product-status.schema';
import { ProductStatusesController } from './product-statuses.controller';
import { ProductStatusesService } from './product-statuses.service';

@Module({
  imports: [MongooseModule.forFeature([{ name: ProductStatus.name, schema: ProductStatusSchema }])],
  controllers: [ProductStatusesController],
  providers: [ProductStatusesService],
  exports: [ProductStatusesService],
})
export class ProductStatusesModule {}
