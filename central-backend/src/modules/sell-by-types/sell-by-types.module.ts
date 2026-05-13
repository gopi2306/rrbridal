import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { SellByType, SellByTypeSchema } from './schemas/sell-by-type.schema';
import { SellByTypesController } from './sell-by-types.controller';
import { SellByTypesService } from './sell-by-types.service';

@Module({
  imports: [MongooseModule.forFeature([{ name: SellByType.name, schema: SellByTypeSchema }])],
  controllers: [SellByTypesController],
  providers: [SellByTypesService],
  exports: [SellByTypesService],
})
export class SellByTypesModule {}
