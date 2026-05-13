import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { OfferGroup, OfferGroupSchema } from './schemas/offer-group.schema';
import { OfferGroupsController } from './offer-groups.controller';
import { OfferGroupsService } from './offer-groups.service';

@Module({
  imports: [MongooseModule.forFeature([{ name: OfferGroup.name, schema: OfferGroupSchema }])],
  controllers: [OfferGroupsController],
  providers: [OfferGroupsService],
  exports: [OfferGroupsService],
})
export class OfferGroupsModule {}
