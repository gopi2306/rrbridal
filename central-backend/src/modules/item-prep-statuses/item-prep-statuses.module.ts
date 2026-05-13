import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { ItemPrepStatus, ItemPrepStatusSchema } from './schemas/item-prep-status.schema';
import { ItemPrepStatusesController } from './item-prep-statuses.controller';
import { ItemPrepStatusesService } from './item-prep-statuses.service';

@Module({
  imports: [MongooseModule.forFeature([{ name: ItemPrepStatus.name, schema: ItemPrepStatusSchema }])],
  controllers: [ItemPrepStatusesController],
  providers: [ItemPrepStatusesService],
  exports: [ItemPrepStatusesService],
})
export class ItemPrepStatusesModule {}
