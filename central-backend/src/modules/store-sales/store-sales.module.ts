import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { StoreDailyExpense, StoreDailyExpenseSchema } from './schemas/store-daily-expense.schema';
import { StoreAdjustment, StoreAdjustmentSchema } from './schemas/store-adjustment.schema';
import { StoreCreditNoteCashout, StoreCreditNoteCashoutSchema } from './schemas/store-credit-note-cashout.schema';
import { StoreCreditNote, StoreCreditNoteSchema } from './schemas/store-credit-note.schema';
import { StoreInvoice, StoreInvoiceSchema } from './schemas/store-invoice.schema';
import { StoreSaleReturn, StoreSaleReturnSchema } from './schemas/store-sale-return.schema';
import { StoreSalesSyncService } from './store-sales-sync.service';

@Module({
  imports: [
    MongooseModule.forFeature([
      { name: StoreInvoice.name, schema: StoreInvoiceSchema },
      { name: StoreSaleReturn.name, schema: StoreSaleReturnSchema },
      { name: StoreAdjustment.name, schema: StoreAdjustmentSchema },
      { name: StoreDailyExpense.name, schema: StoreDailyExpenseSchema },
      { name: StoreCreditNote.name, schema: StoreCreditNoteSchema },
      { name: StoreCreditNoteCashout.name, schema: StoreCreditNoteCashoutSchema },
    ]),
  ],
  providers: [StoreSalesSyncService],
  exports: [StoreSalesSyncService],
})
export class StoreSalesModule {}
