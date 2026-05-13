import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { Colour, ColourSchema } from './schemas/colour.schema';
import { ColoursController } from './colours.controller';
import { ColoursService } from './colours.service';

@Module({
  imports: [MongooseModule.forFeature([{ name: Colour.name, schema: ColourSchema }])],
  controllers: [ColoursController],
  providers: [ColoursService],
  exports: [ColoursService],
})
export class ColoursModule {}
