import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { BatchSelection, BatchSelectionSchema } from './schemas/batch-selection.schema';
import { BatchSelectionsController } from './batch-selections.controller';
import { BatchSelectionsService } from './batch-selections.service';

@Module({
  imports: [MongooseModule.forFeature([{ name: BatchSelection.name, schema: BatchSelectionSchema }])],
  controllers: [BatchSelectionsController],
  providers: [BatchSelectionsService],
  exports: [BatchSelectionsService],
})
export class BatchSelectionsModule {}
