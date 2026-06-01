import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { PromotionSchemesController } from './promotion-schemes.controller';
import { PromotionSchemesService } from './promotion-schemes.service';
import { PromotionScheme, PromotionSchemeSchema } from './schemas/promotion-scheme.schema';

@Module({
  imports: [MongooseModule.forFeature([{ name: PromotionScheme.name, schema: PromotionSchemeSchema }])],
  controllers: [PromotionSchemesController],
  providers: [PromotionSchemesService],
  exports: [PromotionSchemesService],
})
export class PromotionSchemesModule {}
