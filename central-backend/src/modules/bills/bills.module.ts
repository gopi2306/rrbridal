import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { Store, StoreSchema } from '../stores/schemas/store.schema';
import { StoreAdjustment, StoreAdjustmentSchema } from '../store-sales/schemas/store-adjustment.schema';
import { StoreInvoice, StoreInvoiceSchema } from '../store-sales/schemas/store-invoice.schema';
import { StoreSaleReturn, StoreSaleReturnSchema } from '../store-sales/schemas/store-sale-return.schema';
import { BillsController } from './bills.controller';
import { BillsService } from './bills.service';

@Module({
  imports: [
    MongooseModule.forFeature([
      { name: StoreInvoice.name, schema: StoreInvoiceSchema },
      { name: StoreSaleReturn.name, schema: StoreSaleReturnSchema },
      { name: StoreAdjustment.name, schema: StoreAdjustmentSchema },
      { name: Store.name, schema: StoreSchema },
    ]),
  ],
  controllers: [BillsController],
  providers: [BillsService],
})
export class BillsModule {}
