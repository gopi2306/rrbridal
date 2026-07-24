import { BadRequestException, Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model, Types } from 'mongoose';
import { StoresService } from '../stores/stores.service';
import { SyncEvent, SyncEventDocument } from './schemas/sync-event.schema';
import { SyncCursor, SyncCursorDocument } from './schemas/sync-cursor.schema';
import { SyncEventDto } from './dto/sync-push.dto';
import { ProductsService } from '../products/products.service';
import { PurchaseIntentsService } from '../purchase-intents/purchase-intents.service';
import { StockTransfersService } from '../stock-transfers/stock-transfers.service';
import { StoreSalesSyncService } from '../store-sales/store-sales-sync.service';
import { PromotionSchemesService } from '../promotion-schemes/promotion-schemes.service';
import { InventoryAdjustmentsService } from '../inventory-adjustments/inventory-adjustments.service';
import {
  buildProductDeltaFilter,
  encodeProductSyncCursor,
  encodeProductSyncCursorFromProduct,
  parseProductSyncCursor,
} from './product-sync-cursor';

@Injectable()
export class SyncService {
  constructor(
    @InjectModel(SyncEvent.name) private readonly syncEventModel: Model<SyncEventDocument>,
    @InjectModel(SyncCursor.name) private readonly syncCursorModel: Model<SyncCursorDocument>,
    private readonly productsService: ProductsService,
    private readonly purchaseIntentsService: PurchaseIntentsService,
    private readonly storesService: StoresService,
    private readonly stockTransfersService: StockTransfersService,
    private readonly storeSalesSyncService: StoreSalesSyncService,
    private readonly promotionSchemesService: PromotionSchemesService,
    private readonly inventoryAdjustmentsService: InventoryAdjustmentsService,
  ) {}

  async push(events: SyncEventDto[]) {
    const storeIds = [...new Set(events.map((e) => e.storeId))];
    for (const sid of storeIds) {
      const exists = await this.storesService.existsByCode(sid);
      if (!exists) throw new BadRequestException(`Unknown storeId '${sid}'`);
    }

    const results: Array<{ eventId: string; status: 'applied' | 'duplicate' | 'rejected'; reason?: string }> = [];

    for (const ev of events) {
      try {
        const existing = await this.syncEventModel.findOne({ eventId: ev.eventId }).lean();
        if (existing) {
          results.push({ eventId: ev.eventId, status: 'duplicate' });
          continue;
        }

        const meta = { eventId: ev.eventId, storeId: ev.storeId, deviceId: ev.deviceId };

        if (ev.type === 'PurchaseIntentCreated') {
          await this.purchaseIntentsService.ensureFromSync(meta, ev.payload);
        } else if (ev.type === 'StockTransferReceived') {
          await this.stockTransfersService.receiveFromSync(ev.storeId, ev.payload);
        } else if (ev.type === 'InvoiceCreated') {
          await this.storeSalesSyncService.applyInvoiceCreated(meta, ev.payload);
        } else if (ev.type === 'InvoiceDeleted') {
          await this.storeSalesSyncService.applyInvoiceDeleted(meta, ev.payload);
        } else if (ev.type === 'SaleReturnCreated') {
          await this.storeSalesSyncService.applySaleReturn(meta, ev.payload, 'return');
        } else if (ev.type === 'SaleExchangeCreated') {
          await this.storeSalesSyncService.applySaleReturn(meta, ev.payload, 'exchange');
        } else if (ev.type === 'AdjustmentBillCreated') {
          await this.storeSalesSyncService.applyAdjustmentCreated(meta, ev.payload);
        } else if (ev.type === 'CreditNoteCreated') {
          await this.storeSalesSyncService.applyCreditNoteCreated(meta, ev.payload);
        } else if (ev.type === 'CreditNoteApplied') {
          await this.storeSalesSyncService.applyCreditNoteApplied(meta, ev.payload);
        } else if (ev.type === 'CreditNoteCashedOut') {
          await this.storeSalesSyncService.applyCreditNoteCashedOut(meta, ev.payload);
        } else if (ev.type === 'DailyExpenseCreated') {
          await this.storeSalesSyncService.applyDailyExpenseCreated(meta, ev.payload);
        } else if (ev.type === 'DaySessionOpened') {
          await this.storeSalesSyncService.applyDaySessionOpened(meta, ev.payload);
        } else if (ev.type === 'DaySessionClosed') {
          await this.storeSalesSyncService.applyDaySessionClosed(meta, ev.payload);
        } else if (ev.type === 'CashMovementCreated') {
          await this.storeSalesSyncService.applyCashMovementCreated(meta, ev.payload);
        } else if (ev.type === 'InvoiceCodPaymentReceived') {
          await this.storeSalesSyncService.applyInvoiceCodPaymentReceived(meta, ev.payload);
        } else if (ev.type === 'InvoiceCreditPaymentReceived') {
          await this.storeSalesSyncService.applyInvoiceCreditPaymentReceived(meta, ev.payload);
        } else if (ev.type === 'QuotationUpserted') {
          await this.storeSalesSyncService.applyQuotationUpserted(meta, ev.payload);
        } else if (ev.type === 'QuotationConverted') {
          await this.storeSalesSyncService.applyQuotationConverted(meta, ev.payload);
        } else if (ev.type === 'QuotationCancelled') {
          await this.storeSalesSyncService.applyQuotationCancelled(meta, ev.payload);
        } else if (ev.type === 'InventoryAdjustmentCreated') {
          await this.inventoryAdjustmentsService.applyFromSync(meta, ev.payload);
        }

        await this.syncEventModel.create({
          eventId: ev.eventId,
          storeId: ev.storeId,
          deviceId: ev.deviceId,
          type: ev.type,
          createdAt: ev.createdAt,
          payload: ev.payload,
          hash: ev.hash,
          status: 'applied',
        } satisfies Partial<SyncEvent>);

        // Cursor strategy (simple scaffold):
        // use Mongo ObjectId ordering implicitly; cursor is last created _id string.
        await this.syncCursorModel.updateOne(
          { storeId: ev.storeId },
          { $setOnInsert: { storeId: ev.storeId }, $set: { lastSuccessAt: new Date().toISOString() } },
          { upsert: true },
        );

        results.push({ eventId: ev.eventId, status: 'applied' });
      } catch (err) {
        results.push({ eventId: ev.eventId, status: 'rejected', reason: err instanceof Error ? err.message : 'unknown' });
      }
    }

    return results;
  }

  async pull(
    storeId: string,
    sinceCursor: string,
    limit: number,
    sinceTransferCursor = '0',
    sincePromotionCursor = '0',
    sinceAdjustmentCursor = '0',
  ) {
    // Product deltas use compound cursor `{updatedAtIso}|{objectId}` so price/master
    // edits re-sync. Legacy bare ObjectId triggers a one-time full product catch-up.
    const productCursorParsed = parseProductSyncCursor(sinceCursor);
    const cursorFilter = buildProductDeltaFilter(productCursorParsed);

    const products = await this.productsService.listDeltas(cursorFilter, limit);
    const transfers = await this.stockTransfersService.listAwaitingIntakeForStore(storeId, limit);
    const completedTransfers = await this.stockTransfersService.listCompletedForStorePullWindow(
      storeId,
      sinceTransferCursor,
      limit,
    );

    const promotionCursorFilter =
      sincePromotionCursor && sincePromotionCursor !== '0' && Types.ObjectId.isValid(sincePromotionCursor)
        ? { _id: { $gt: new Types.ObjectId(sincePromotionCursor) } }
        : {};
    const promotionSchemes = await this.promotionSchemesService.listDeltasForStore(
      storeId,
      promotionCursorFilter,
      limit,
    );
    const storeAdjustments = await this.inventoryAdjustmentsService.listForStorePull(
      storeId,
      sinceAdjustmentCursor,
      limit,
    );
    const last = products.length > 0 ? products[products.length - 1] : undefined;
    let cursor = sinceCursor ?? '0';
    if (products.length > 0) {
      cursor = encodeProductSyncCursorFromProduct(
        last as { _id?: unknown; updatedAt?: unknown },
        sinceCursor ?? '0',
      );
    } else if (productCursorParsed.kind === 'full-catchup') {
      // Upgrade away from legacy bare ObjectId even when the catalog is empty.
      cursor = encodeProductSyncCursor(new Date(0), new Types.ObjectId('000000000000000000000000'));
    }

    const productUpdates = products.map((p) => ({
      type: 'ProductUpserted',
      storeId,
      createdAt: new Date().toISOString(),
      payload: { product: p },
    }));

    const transferUpdates = transfers.map((transfer) => {
      const row = transfer as typeof transfer & { _id?: unknown; updatedAt?: Date };
      return {
        type: 'StockTransferAwaitingStoreIntake',
        storeId,
        createdAt: row.updatedAt instanceof Date ? row.updatedAt.toISOString() : new Date().toISOString(),
        payload: {
          transfer: this.stockTransfersService.toSyncTransferPayload(
            transfer as Record<string, unknown> & { _id?: unknown },
          ),
        },
      };
    });

    const completedTransferUpdates = completedTransfers.map((transfer) => {
      const row = transfer as typeof transfer & { _id?: unknown; updatedAt?: Date };
      return {
        type: 'StockTransferCompleted',
        storeId,
        createdAt: row.updatedAt instanceof Date ? row.updatedAt.toISOString() : new Date().toISOString(),
        payload: {
          transfer: this.stockTransfersService.toSyncTransferPayload(
            transfer as Record<string, unknown> & { _id?: unknown },
          ),
        },
      };
    });

    const promotionUpdates = promotionSchemes.map((scheme) => {
      const row = scheme as typeof scheme & {
        _id?: unknown;
        updatedAt?: Date;
        deletedAt?: Date;
        isActive?: boolean;
        code?: string;
      };
      const isDeleted = Boolean(row.deletedAt) || row.isActive === false;
      return {
        type: isDeleted ? 'PromotionSchemeDeleted' : 'PromotionSchemeUpserted',
        storeId,
        createdAt: row.updatedAt instanceof Date ? row.updatedAt.toISOString() : new Date().toISOString(),
        payload: isDeleted
          ? { schemeId: String(row._id), code: row.code }
          : { scheme },
      };
    });

    const adjustmentUpdates = storeAdjustments.map((adjustment) => {
      const row = adjustment as typeof adjustment & { _id?: unknown; updatedAt?: Date; createdAt?: Date };
      return {
        type: 'StoreInventoryAdjusted',
        storeId,
        createdAt:
          row.updatedAt instanceof Date
            ? row.updatedAt.toISOString()
            : row.createdAt instanceof Date
              ? row.createdAt.toISOString()
              : new Date().toISOString(),
        payload: {
          adjustment: this.inventoryAdjustmentsService.toSyncPayload(
            row as unknown as Record<string, unknown>,
          ),
        },
      };
    });

    let transferCursor = sinceTransferCursor ?? '0';
    if (completedTransfers.length > 0) {
      const lastT = completedTransfers[completedTransfers.length - 1] as { _id?: unknown };
      transferCursor = lastT._id ? String(lastT._id) : transferCursor;
    }

    let promotionCursor = sincePromotionCursor ?? '0';
    if (promotionSchemes.length > 0) {
      const lastP = promotionSchemes[promotionSchemes.length - 1] as { _id?: unknown };
      promotionCursor = lastP._id ? String(lastP._id) : promotionCursor;
    }

    let adjustmentCursor = sinceAdjustmentCursor ?? '0';
    if (storeAdjustments.length > 0) {
      const lastA = storeAdjustments[storeAdjustments.length - 1] as { _id?: unknown };
      adjustmentCursor = lastA._id ? String(lastA._id) : adjustmentCursor;
    }

    await this.syncCursorModel.updateOne(
      { storeId },
      { $setOnInsert: { storeId }, $set: { cursor, lastSuccessAt: new Date().toISOString() } },
      { upsert: true },
    );

    return {
      cursor,
      transferCursor,
      promotionCursor,
      adjustmentCursor,
      updates: [
        ...productUpdates,
        ...transferUpdates,
        ...completedTransferUpdates,
        ...adjustmentUpdates,
        ...promotionUpdates,
      ],
    };
  }
}

