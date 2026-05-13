import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { IndentType, IndentTypeSchema } from './schemas/indent-type.schema';
import { IndentTypesController } from './indent-types.controller';
import { IndentTypesService } from './indent-types.service';

@Module({
  imports: [MongooseModule.forFeature([{ name: IndentType.name, schema: IndentTypeSchema }])],
  controllers: [IndentTypesController],
  providers: [IndentTypesService],
  exports: [IndentTypesService],
})
export class IndentTypesModule {}
