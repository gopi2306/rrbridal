import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { DocumentNumbersModule } from '../document-numbers/document-numbers.module';
import { Branch, BranchSchema } from '../branches/schemas/branch.schema';
import { Division, DivisionSchema } from '../divisions/schemas/division.schema';
import { Location, LocationSchema } from '../locations/schemas/location.schema';
import { Product, ProductSchema } from '../products/schemas/product.schema';
import { Supplier, SupplierSchema } from '../suppliers/schemas/supplier.schema';
import { PurchaseReturn, PurchaseReturnSchema } from './schemas/purchase-return.schema';
import { PurchaseReturnsController } from './purchase-returns.controller';
import { PurchaseReturnsService } from './purchase-returns.service';

@Module({
  imports: [
    DocumentNumbersModule,
    MongooseModule.forFeature([
      { name: PurchaseReturn.name, schema: PurchaseReturnSchema },
      { name: Branch.name, schema: BranchSchema },
      { name: Location.name, schema: LocationSchema },
      { name: Division.name, schema: DivisionSchema },
      { name: Supplier.name, schema: SupplierSchema },
      { name: Product.name, schema: ProductSchema },
    ]),
  ],
  controllers: [PurchaseReturnsController],
  providers: [PurchaseReturnsService],
  exports: [PurchaseReturnsService],
})
export class PurchaseReturnsModule {}

