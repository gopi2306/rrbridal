import { Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { PurchaseOrder, PurchaseOrderDocument } from '../purchase-orders/schemas/purchase-order.schema';
import { GoodsReceipt, GoodsReceiptDocument } from '../goods-receipts/schemas/goods-receipt.schema';
import { StockTransfer, StockTransferDocument } from '../stock-transfers/schemas/stock-transfer.schema';
import { Supplier, SupplierDocument } from '../suppliers/schemas/supplier.schema';
import { InventoryLedgerEntry, InventoryLedgerDocument } from '../inventory/schemas/inventory-ledger.schema';

export interface DashboardSummary {
  openPOs: number;
  pendingReceipts: number;
  transfersToday: number;
  activeSuppliers: number;
}

export interface RecentPO {
  id: string;
  poNo: string;
  supplierName: string;
  itemCount: number;
  expectedDate: string | null;
}

export interface WarehouseAlert {
  type: 'price_mismatch' | 'low_stock' | 'pending_qc';
  title: string;
  description: string;
  count: number;
}

export interface DashboardResponse {
  summary: DashboardSummary;
  recentPurchaseOrders: RecentPO[];
  warehouseAlerts: WarehouseAlert[];
}

@Injectable()
export class DashboardService {
  constructor(
    @InjectModel(PurchaseOrder.name) private readonly poModel: Model<PurchaseOrderDocument>,
    @InjectModel(GoodsReceipt.name) private readonly grModel: Model<GoodsReceiptDocument>,
    @InjectModel(StockTransfer.name) private readonly stModel: Model<StockTransferDocument>,
    @InjectModel(Supplier.name) private readonly supplierModel: Model<SupplierDocument>,
    @InjectModel(InventoryLedgerEntry.name) private readonly ledgerModel: Model<InventoryLedgerDocument>,
  ) {}

  async getDashboard(): Promise<DashboardResponse> {
    const [summary, recentPurchaseOrders, warehouseAlerts] = await Promise.all([
      this.getSummary(),
      this.getRecentPOs(),
      this.getWarehouseAlerts(),
    ]);

    return { summary, recentPurchaseOrders, warehouseAlerts };
  }

  private async getSummary(): Promise<DashboardSummary> {
    const todayStart = new Date();
    todayStart.setHours(0, 0, 0, 0);

    const [openPOs, pendingReceipts, transfersToday, activeSuppliers] = await Promise.all([
      this.poModel.countDocuments({ status: { $in: ['open', 'awaiting_approval', 'approved'] } }),
      this.grModel.countDocuments({ status: 'draft' }),
      this.stModel.countDocuments({ createdAt: { $gte: todayStart }, status: { $ne: 'cancelled' } }),
      this.supplierModel.countDocuments({ isActive: true, isSupplier: true }),
    ]);

    return { openPOs, pendingReceipts, transfersToday, activeSuppliers };
  }

  private async getRecentPOs(): Promise<RecentPO[]> {
    const docs = await this.poModel
      .find({ status: { $in: ['open', 'awaiting_approval', 'approved', 'partially_received'] } })
      .sort({ createdAt: -1 })
      .limit(10)
      .lean();

    return docs.map((doc) => ({
      id: String(doc._id),
      poNo: doc.poNo,
      supplierName: doc.supplier?.name ?? doc.supplier?.shortname ?? 'Unknown',
      itemCount: doc.lines?.length ?? 0,
      expectedDate: doc.deliveryDate ?? null,
    }));
  }

  private async getWarehouseAlerts(): Promise<WarehouseAlert[]> {
    const alerts: WarehouseAlert[] = [];

    const lowStockSkus = await this.getLowStockCount();
    if (lowStockSkus > 0) {
      alerts.push({
        type: 'low_stock',
        title: 'Low stock in store',
        description: `${lowStockSkus} SKUs below minimum threshold`,
        count: lowStockSkus,
      });
    }

    const pendingQcCount = await this.grModel.countDocuments({ status: 'draft' });
    if (pendingQcCount > 0) {
      alerts.push({
        type: 'pending_qc',
        title: 'Pending QC',
        description: `${pendingQcCount} invoices need item checks`,
        count: pendingQcCount,
      });
    }

    return alerts;
  }

  private async getLowStockCount(): Promise<number> {
    const threshold = 5;

    const rows = await this.ledgerModel.aggregate([
      { $match: { locationKind: { $in: ['store', null] } } },
      { $group: { _id: '$sku', totalQty: { $sum: '$qtyDelta' } } },
      { $match: { totalQty: { $gt: 0, $lte: threshold } } },
      { $count: 'count' },
    ]);

    const first = rows[0] as { count: number } | undefined;
    return first?.count ?? 0;
  }
}
