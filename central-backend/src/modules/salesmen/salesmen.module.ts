import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { DocumentNumbersModule } from '../document-numbers/document-numbers.module';
import { SalesmanCodeGenerator } from './salesman-code.generator';
import { Salesman, SalesmanSchema } from './schemas/salesman.schema';
import { SalesmenController } from './salesmen.controller';
import { SalesmenService } from './salesmen.service';

@Module({
  imports: [
    DocumentNumbersModule,
    MongooseModule.forFeature([{ name: Salesman.name, schema: SalesmanSchema }]),
  ],
  controllers: [SalesmenController],
  providers: [SalesmenService, SalesmanCodeGenerator],
  exports: [SalesmenService],
})
export class SalesmenModule {}
