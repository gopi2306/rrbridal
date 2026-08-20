import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { InventoryModule } from '../inventory/inventory.module';
import { ProductsModule } from '../products/products.module';
import { StoreCashMovement, StoreCashMovementSchema } from '../store-sales/schemas/store-cash-movement.schema';
import { StoreCreditNote, StoreCreditNoteSchema } from '../store-sales/schemas/store-credit-note.schema';
import { StoreCreditNoteCashout, StoreCreditNoteCashoutSchema } from '../store-sales/schemas/store-credit-note-cashout.schema';
import { StoreDailyExpense, StoreDailyExpenseSchema } from '../store-sales/schemas/store-daily-expense.schema';
import { StoreDayClose, StoreDayCloseSchema } from '../store-sales/schemas/store-day-close.schema';
import { StoreInvoice, StoreInvoiceSchema } from '../store-sales/schemas/store-invoice.schema';
import { StoreAdjustment, StoreAdjustmentSchema } from '../store-sales/schemas/store-adjustment.schema';
import { StorePaymentReceipt, StorePaymentReceiptSchema } from '../store-sales/schemas/store-payment-receipt.schema';
import { StoreGatewayPayment, StoreGatewayPaymentSchema } from '../store-sales/schemas/store-gateway-payment.schema';
import { StoreQuotation, StoreQuotationSchema } from '../store-sales/schemas/store-quotation.schema';
import { StoreSaleReturn, StoreSaleReturnSchema } from '../store-sales/schemas/store-sale-return.schema';
import { StoresModule } from '../stores/stores.module';
import { SyncModule } from '../sync/sync.module';
import { PromotionSchemesModule } from '../promotion-schemes/promotion-schemes.module';
import { StockTransfersModule } from '../stock-transfers/stock-transfers.module';
import { OutboundDispatchesModule } from '../outbound-dispatches/outbound-dispatches.module';
import { OutboundDispatchesController } from '../outbound-dispatches/outbound-dispatches.controller';
import { StoreHeldBill, StoreHeldBillSchema } from './schemas/store-held-bill.schema';
import { StorePosCounter, StorePosCounterSchema } from './schemas/store-pos-counter.schema';
import { StorePosController } from './store-pos.controller';
import { StorePosQueryService } from './store-pos-query.service';
import { StorePosService } from './store-pos.service';

@Module({
  imports: [
    SyncModule,
    ProductsModule,
    InventoryModule,
    StoresModule,
    PromotionSchemesModule,
    StockTransfersModule,
    OutboundDispatchesModule,
    MongooseModule.forFeature([
      { name: StoreInvoice.name, schema: StoreInvoiceSchema },
      { name: StoreSaleReturn.name, schema: StoreSaleReturnSchema },
      { name: StoreQuotation.name, schema: StoreQuotationSchema },
      { name: StoreCreditNote.name, schema: StoreCreditNoteSchema },
      { name: StoreCreditNoteCashout.name, schema: StoreCreditNoteCashoutSchema },
      { name: StoreDayClose.name, schema: StoreDayCloseSchema },
      { name: StoreCashMovement.name, schema: StoreCashMovementSchema },
      { name: StoreDailyExpense.name, schema: StoreDailyExpenseSchema },
      { name: StoreAdjustment.name, schema: StoreAdjustmentSchema },
      { name: StorePaymentReceipt.name, schema: StorePaymentReceiptSchema },
      { name: StoreGatewayPayment.name, schema: StoreGatewayPaymentSchema },
      { name: StorePosCounter.name, schema: StorePosCounterSchema },
      { name: StoreHeldBill.name, schema: StoreHeldBillSchema },
    ]),
  ],
  controllers: [StorePosController, OutboundDispatchesController],
  providers: [StorePosService, StorePosQueryService],
  exports: [StorePosService, StorePosQueryService],
})
export class StorePosModule {}
