import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { GstUom, GstUomSchema } from './schemas/gst-uom.schema';
import { GstUomsController } from './gst-uoms.controller';
import { GstUomsService } from './gst-uoms.service';

@Module({
  imports: [MongooseModule.forFeature([{ name: GstUom.name, schema: GstUomSchema }])],
  controllers: [GstUomsController],
  providers: [GstUomsService],
  exports: [GstUomsService],
})
export class GstUomsModule {}
