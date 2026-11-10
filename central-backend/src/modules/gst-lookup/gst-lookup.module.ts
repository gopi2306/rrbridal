import { Module } from '@nestjs/common';
import { GstLookupController } from './gst-lookup.controller';
import { GstLookupService } from './gst-lookup.service';

@Module({
  controllers: [GstLookupController],
  providers: [GstLookupService],
  exports: [GstLookupService],
})
export class GstLookupModule {}
