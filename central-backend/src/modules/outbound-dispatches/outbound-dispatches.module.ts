import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import {
  StoreDailyExpense,
  StoreDailyExpenseSchema,
} from '../store-sales/schemas/store-daily-expense.schema';
import {
  StorePaymentReceipt,
  StorePaymentReceiptSchema,
} from '../store-sales/schemas/store-payment-receipt.schema';
import {
  StoreInvoice,
  StoreInvoiceSchema,
} from '../store-sales/schemas/store-invoice.schema';
import { OutboundDispatchesService } from './outbound-dispatches.service';
import {
  OutboundDispatch,
  OutboundDispatchSchema,
} from './schemas/outbound-dispatch.schema';

@Module({
  imports: [
    MongooseModule.forFeature([
      { name: OutboundDispatch.name, schema: OutboundDispatchSchema },
      { name: StorePaymentReceipt.name, schema: StorePaymentReceiptSchema },
      { name: StoreDailyExpense.name, schema: StoreDailyExpenseSchema },
      { name: StoreInvoice.name, schema: StoreInvoiceSchema },
    ]),
  ],
  providers: [OutboundDispatchesService],
  exports: [OutboundDispatchesService],
})
export class OutboundDispatchesModule {}
