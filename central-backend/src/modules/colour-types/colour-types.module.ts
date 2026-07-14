import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { ColourType, ColourTypeSchema } from './schemas/colour-type.schema';
import { ColourTypesController } from './colour-types.controller';
import { ColourTypesService } from './colour-types.service';

@Module({
  imports: [MongooseModule.forFeature([{ name: ColourType.name, schema: ColourTypeSchema }])],
  controllers: [ColourTypesController],
  providers: [ColourTypesService],
  exports: [ColourTypesService],
})
export class ColourTypesModule {}
