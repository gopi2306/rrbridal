import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { PackedConfirmation, PackedConfirmationSchema } from './schemas/packed-confirmation.schema';
import { PackedConfirmationsController } from './packed-confirmations.controller';
import { PackedConfirmationsService } from './packed-confirmations.service';

@Module({
  imports: [MongooseModule.forFeature([{ name: PackedConfirmation.name, schema: PackedConfirmationSchema }])],
  controllers: [PackedConfirmationsController],
  providers: [PackedConfirmationsService],
  exports: [PackedConfirmationsService],
})
export class PackedConfirmationsModule {}
