import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { WeightSize, WeightSizeSchema } from './schemas/weight-size.schema';
import { WeightSizesController } from './weight-sizes.controller';
import { WeightSizesService } from './weight-sizes.service';

@Module({
  imports: [MongooseModule.forFeature([{ name: WeightSize.name, schema: WeightSizeSchema }])],
  controllers: [WeightSizesController],
  providers: [WeightSizesService],
  exports: [WeightSizesService],
})
export class WeightSizesModule {}
