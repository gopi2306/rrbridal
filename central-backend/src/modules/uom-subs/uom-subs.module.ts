import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { UomSub, UomSubSchema } from './schemas/uom-sub.schema';
import { UomSubsController } from './uom-subs.controller';
import { UomSubsService } from './uom-subs.service';

@Module({
  imports: [MongooseModule.forFeature([{ name: UomSub.name, schema: UomSubSchema }])],
  controllers: [UomSubsController],
  providers: [UomSubsService],
  exports: [UomSubsService],
})
export class UomSubsModule {}
