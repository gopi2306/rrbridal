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

@Injectable()
export class SyncService {
  constructor(
    @InjectModel(SyncEvent.name) private readonly syncEventModel: Model<SyncEventDocument>,
    @InjectModel(SyncCursor.name) private readonly syncCursorModel: Model<SyncCursorDocument>,
    private readonly productsService: ProductsService,
    private readonly purchaseIntentsService: PurchaseIntentsService,
    private readonly storesService: StoresService,
    private readonly stockTransfersService: StockTransfersService,
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

        if (ev.type === 'PurchaseIntentCreated') {
          await this.purchaseIntentsService.ensureFromSync(
            { eventId: ev.eventId, storeId: ev.storeId, deviceId: ev.deviceId },
            ev.payload,
          );
        } else if (ev.type === 'StockTransferReceived') {
          await this.stockTransfersService.receiveFromSync(ev.storeId, ev.payload);
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

  async pull(storeId: string, sinceCursor: string, limit: number, sinceTransferCursor = '0') {
    // Upgrade: for now, sync pull delivers ProductUpserted deltas from central products.
    // Cursor is an ObjectId string corresponding to the last product _id sent.
    const cursorFilter =
      sinceCursor && sinceCursor !== '0' && Types.ObjectId.isValid(sinceCursor)
        ? { _id: { $gt: new Types.ObjectId(sinceCursor) } }
        : {};

    const products = await this.productsService.listDeltas(cursorFilter, limit);
    const transfers = await this.stockTransfersService.listAwaitingIntakeForStore(storeId, limit);
    const completedTransfers = await this.stockTransfersService.listCompletedForStorePullWindow(
      storeId,
      sinceTransferCursor,
      limit,
    );
    const last = products.length > 0 ? products[products.length - 1] : undefined;
    const cursor = last?._id ? String(last._id) : sinceCursor ?? '0';

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

    let transferCursor = sinceTransferCursor ?? '0';
    if (completedTransfers.length > 0) {
      const lastT = completedTransfers[completedTransfers.length - 1] as { _id?: unknown };
      transferCursor = lastT._id ? String(lastT._id) : transferCursor;
    }

    await this.syncCursorModel.updateOne(
      { storeId },
      { $setOnInsert: { storeId }, $set: { cursor, lastSuccessAt: new Date().toISOString() } },
      { upsert: true },
    );

    return {
      cursor,
      transferCursor,
      updates: [...productUpdates, ...transferUpdates, ...completedTransferUpdates],
    };
  }
}

