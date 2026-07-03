import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { InventoryModule } from '../inventory/inventory.module';
import { InventoryLedgerEntry, InventoryLedgerSchema } from '../inventory/schemas/inventory-ledger.schema';
import { StoreDailyExpense, StoreDailyExpenseSchema } from './schemas/store-daily-expense.schema';
import { StoreDayClose, StoreDayCloseSchema } from './schemas/store-day-close.schema';
import { StoreCashMovement, StoreCashMovementSchema } from './schemas/store-cash-movement.schema';
import { StoreAdjustment, StoreAdjustmentSchema } from './schemas/store-adjustment.schema';
import { StoreCreditNoteCashout, StoreCreditNoteCashoutSchema } from './schemas/store-credit-note-cashout.schema';
import { StoreCreditNote, StoreCreditNoteSchema } from './schemas/store-credit-note.schema';
import { StoreInvoice, StoreInvoiceSchema } from './schemas/store-invoice.schema';
import { StoreSaleReturn, StoreSaleReturnSchema } from './schemas/store-sale-return.schema';
import { StoreSalesInventoryController } from './store-sales-inventory.controller';
import { StoreSalesInventoryService } from './store-sales-inventory.service';
import { StoreSalesSyncService } from './store-sales-sync.service';

@Module({
  imports: [
    InventoryModule,
    MongooseModule.forFeature([
      { name: InventoryLedgerEntry.name, schema: InventoryLedgerSchema },
      { name: StoreInvoice.name, schema: StoreInvoiceSchema },
      { name: StoreSaleReturn.name, schema: StoreSaleReturnSchema },
      { name: StoreAdjustment.name, schema: StoreAdjustmentSchema },
      { name: StoreDailyExpense.name, schema: StoreDailyExpenseSchema },
      { name: StoreDayClose.name, schema: StoreDayCloseSchema },
      { name: StoreCashMovement.name, schema: StoreCashMovementSchema },
      { name: StoreCreditNote.name, schema: StoreCreditNoteSchema },
      { name: StoreCreditNoteCashout.name, schema: StoreCreditNoteCashoutSchema },
    ]),
  ],
  controllers: [StoreSalesInventoryController],
  providers: [StoreSalesSyncService, StoreSalesInventoryService],
  exports: [StoreSalesSyncService, StoreSalesInventoryService],
})
export class StoreSalesModule {}
