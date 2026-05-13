import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { SkuType, SkuTypeSchema } from './schemas/sku-type.schema';
import { SkuTypesController } from './sku-types.controller';
import { SkuTypesService } from './sku-types.service';

@Module({
  imports: [MongooseModule.forFeature([{ name: SkuType.name, schema: SkuTypeSchema }])],
  controllers: [SkuTypesController],
  providers: [SkuTypesService],
  exports: [SkuTypesService],
})
export class SkuTypesModule {}
