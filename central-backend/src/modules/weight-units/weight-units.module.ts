import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { WeightUnit, WeightUnitSchema } from './schemas/weight-unit.schema';
import { WeightUnitsController } from './weight-units.controller';
import { WeightUnitsService } from './weight-units.service';

@Module({
  imports: [MongooseModule.forFeature([{ name: WeightUnit.name, schema: WeightUnitSchema }])],
  controllers: [WeightUnitsController],
  providers: [WeightUnitsService],
  exports: [WeightUnitsService],
})
export class WeightUnitsModule {}
