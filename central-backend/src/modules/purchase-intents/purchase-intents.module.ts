import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { PurchaseIntent, PurchaseIntentSchema } from './schemas/purchase-intent.schema';
import { PurchaseIntentsController } from './purchase-intents.controller';
import { PurchaseIntentsService } from './purchase-intents.service';

@Module({
  imports: [MongooseModule.forFeature([{ name: PurchaseIntent.name, schema: PurchaseIntentSchema }])],
  controllers: [PurchaseIntentsController],
  providers: [PurchaseIntentsService],
  exports: [PurchaseIntentsService],
})
export class PurchaseIntentsModule {}
