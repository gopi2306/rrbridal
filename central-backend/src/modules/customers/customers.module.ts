import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { DocumentNumbersModule } from '../document-numbers/document-numbers.module';
import { CustomerImportController } from './customer-import.controller';
import { CustomerCodeGenerator } from './customer-code.generator';
import { Customer, CustomerSchema } from './schemas/customer.schema';
import { CustomerImportService } from './import/customer-import.service';
import { CustomersController } from './customers.controller';
import { CustomersService } from './customers.service';

@Module({
  imports: [
    DocumentNumbersModule,
    MongooseModule.forFeature([{ name: Customer.name, schema: CustomerSchema }]),
  ],
  controllers: [CustomersController, CustomerImportController],
  providers: [CustomersService, CustomerImportService, CustomerCodeGenerator],
  exports: [CustomersService],
})
export class CustomersModule {}
