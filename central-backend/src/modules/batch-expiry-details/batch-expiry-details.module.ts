import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { BatchExpiryDetail, BatchExpiryDetailSchema } from './schemas/batch-expiry-detail.schema';
import { BatchExpiryDetailsController } from './batch-expiry-details.controller';
import { BatchExpiryDetailsService } from './batch-expiry-details.service';

@Module({
  imports: [MongooseModule.forFeature([{ name: BatchExpiryDetail.name, schema: BatchExpiryDetailSchema }])],
  controllers: [BatchExpiryDetailsController],
  providers: [BatchExpiryDetailsService],
  exports: [BatchExpiryDetailsService],
})
export class BatchExpiryDetailsModule {}
