import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { SkuOrderGroup, SkuOrderGroupSchema } from './schemas/sku-order-group.schema';
import { SkuOrderGroupsController } from './sku-order-groups.controller';
import { SkuOrderGroupsService } from './sku-order-groups.service';

@Module({
  imports: [MongooseModule.forFeature([{ name: SkuOrderGroup.name, schema: SkuOrderGroupSchema }])],
  controllers: [SkuOrderGroupsController],
  providers: [SkuOrderGroupsService],
  exports: [SkuOrderGroupsService],
})
export class SkuOrderGroupsModule {}
