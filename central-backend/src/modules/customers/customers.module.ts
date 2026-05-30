import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { CustomerImportController } from './customer-import.controller';
import { Customer, CustomerSchema } from './schemas/customer.schema';
import { CustomerImportService } from './import/customer-import.service';
import { CustomersController } from './customers.controller';
import { CustomersService } from './customers.service';

@Module({
  imports: [MongooseModule.forFeature([{ name: Customer.name, schema: CustomerSchema }])],
  controllers: [CustomersController, CustomerImportController],
  providers: [CustomersService, CustomerImportService],
  exports: [CustomersService],
})
export class CustomersModule {}
