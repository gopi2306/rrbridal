import { BadRequestException, Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model, Types } from 'mongoose';
import { StoresService } from '../stores/stores.service';
import { SyncEvent, SyncEventDocument } from './schemas/sync-event.schema';
import { SyncCursor, SyncCursorDocument } from './schemas/sync-cursor.schema';
import { SyncEventDto } from './dto/sync-push.dto';
import { ProductsService } from '../products/products.service';
import { PurchaseIntentsService } from '../purchase-intents/purchase-intents.service';

@Injectable()
export class SyncService {
  constructor(
    @InjectModel(SyncEvent.name) private readonly syncEventModel: Model<SyncEventDocument>,
    @InjectModel(SyncCursor.name) private readonly syncCursorModel: Model<SyncCursorDocument>,
    private readonly productsService: ProductsService,
    private readonly purchaseIntentsService: PurchaseIntentsService,
    private readonly storesService: StoresService,
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

  async pull(storeId: string, sinceCursor: string, limit: number) {
    // Upgrade: for now, sync pull delivers ProductUpserted deltas from central products.
    // Cursor is an ObjectId string corresponding to the last product _id sent.
    const cursorFilter =
      sinceCursor && sinceCursor !== '0' && Types.ObjectId.isValid(sinceCursor)
        ? { _id: { $gt: new Types.ObjectId(sinceCursor) } }
        : {};

    const products = await this.productsService.listDeltas(cursorFilter, limit);
    const last = products.length > 0 ? products[products.length - 1] : undefined;
    const cursor = last?._id ? String(last._id) : sinceCursor ?? '0';

    const updates = products.map((p) => ({
      type: 'ProductUpserted',
      storeId,
      createdAt: new Date().toISOString(),
      payload: { product: p },
    }));

    await this.syncCursorModel.updateOne(
      { storeId },
      { $setOnInsert: { storeId }, $set: { cursor, lastSuccessAt: new Date().toISOString() } },
      { upsert: true },
    );

    return { cursor, updates };
  }
}

