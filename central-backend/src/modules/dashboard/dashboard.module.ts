import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { PurchaseOrder, PurchaseOrderSchema } from '../purchase-orders/schemas/purchase-order.schema';
import { GoodsReceipt, GoodsReceiptSchema } from '../goods-receipts/schemas/goods-receipt.schema';
import { StockTransfer, StockTransferSchema } from '../stock-transfers/schemas/stock-transfer.schema';
import { Supplier, SupplierSchema } from '../suppliers/schemas/supplier.schema';
import { InventoryLedgerEntry, InventoryLedgerSchema } from '../inventory/schemas/inventory-ledger.schema';
import { DashboardController } from './dashboard.controller';
import { DashboardService } from './dashboard.service';

@Module({
  imports: [
    MongooseModule.forFeature([
      { name: PurchaseOrder.name, schema: PurchaseOrderSchema },
      { name: GoodsReceipt.name, schema: GoodsReceiptSchema },
      { name: StockTransfer.name, schema: StockTransferSchema },
      { name: Supplier.name, schema: SupplierSchema },
      { name: InventoryLedgerEntry.name, schema: InventoryLedgerSchema },
    ]),
  ],
  controllers: [DashboardController],
  providers: [DashboardService],
})
export class DashboardModule {}
