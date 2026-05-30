import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { Supplier, SupplierSchema } from './schemas/supplier.schema';
import { SupplierImportService } from './import/supplier-import.service';
import { SupplierImportController } from './supplier-import.controller';
import { SuppliersController } from './suppliers.controller';
import { SuppliersService } from './suppliers.service';

@Module({
  imports: [MongooseModule.forFeature([{ name: Supplier.name, schema: SupplierSchema }])],
  controllers: [SuppliersController, SupplierImportController],
  providers: [SuppliersService, SupplierImportService],
  exports: [SuppliersService],
})
export class SuppliersModule {}

