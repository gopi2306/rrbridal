import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { AuthModule } from '../auth/auth.module';
import { BarcodeLabelDesignsModule } from '../barcode-label-designs/barcode-label-design.module';
import { BillsModule } from '../bills/bills.module';
import { Brand, BrandSchema } from '../brands/schemas/brand.schema';
import { Category, CategorySchema } from '../categories/schemas/category.schema';
import { CustomersModule } from '../customers/customers.module';
import { InventoryAdjustmentsModule } from '../inventory-adjustments/inventory-adjustments.module';
import { InventoryModule } from '../inventory/inventory.module';
import { Product, ProductSchema } from '../products/schemas/product.schema';
import { SalesmenModule } from '../salesmen/salesmen.module';
import {
  StoreCashMovement,
  StoreCashMovementSchema,
} from '../store-sales/schemas/store-cash-movement.schema';
import {
  StoreCreditNote,
  StoreCreditNoteSchema,
} from '../store-sales/schemas/store-credit-note.schema';
import {
  StoreDailyExpense,
  StoreDailyExpenseSchema,
} from '../store-sales/schemas/store-daily-expense.schema';
import { StoreDayClose, StoreDayCloseSchema } from '../store-sales/schemas/store-day-close.schema';
import { StoreInvoice, StoreInvoiceSchema } from '../store-sales/schemas/store-invoice.schema';
import { StoreQuotation, StoreQuotationSchema } from '../store-sales/schemas/store-quotation.schema';
import {
  StoreSaleReturn,
  StoreSaleReturnSchema,
} from '../store-sales/schemas/store-sale-return.schema';
import { StoreSalesModule } from '../store-sales/store-sales.module';
import { StoresModule } from '../stores/stores.module';
import { UsersModule } from '../users/users.module';
import { WhatsAppModule } from '../whatsapp/whatsapp.module';
import { PosV2Controller } from './pos-v2.controller';
import { PosV2Service } from './pos-v2.service';
import {
  CompanyBillingSettings,
  CompanyBillingSettingsSchema,
} from './schemas/company-billing-settings.schema';

@Module({
  imports: [
    AuthModule,
    UsersModule,
    StoresModule,
    InventoryModule,
    BillsModule,
    StoreSalesModule,
    CustomersModule,
    SalesmenModule,
    InventoryAdjustmentsModule,
    BarcodeLabelDesignsModule,
    WhatsAppModule,
    MongooseModule.forFeature([
      { name: CompanyBillingSettings.name, schema: CompanyBillingSettingsSchema },
      { name: Product.name, schema: ProductSchema },
      { name: Category.name, schema: CategorySchema },
      { name: Brand.name, schema: BrandSchema },
      { name: StoreInvoice.name, schema: StoreInvoiceSchema },
      { name: StoreQuotation.name, schema: StoreQuotationSchema },
      { name: StoreSaleReturn.name, schema: StoreSaleReturnSchema },
      { name: StoreDayClose.name, schema: StoreDayCloseSchema },
      { name: StoreCashMovement.name, schema: StoreCashMovementSchema },
      { name: StoreDailyExpense.name, schema: StoreDailyExpenseSchema },
      { name: StoreCreditNote.name, schema: StoreCreditNoteSchema },
    ]),
  ],
  controllers: [PosV2Controller],
  providers: [PosV2Service, JwtAuthGuard],
})
export class PosV2Module {}
