import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { HsnCode, HsnCodeSchema } from './schemas/hsn-code.schema';
import { HsnCodesController } from './hsn-codes.controller';
import { HsnCodesService } from './hsn-codes.service';

@Module({
  imports: [MongooseModule.forFeature([{ name: HsnCode.name, schema: HsnCodeSchema }])],
  controllers: [HsnCodesController],
  providers: [HsnCodesService],
  exports: [HsnCodesService],
})
export class HsnCodesModule {}
