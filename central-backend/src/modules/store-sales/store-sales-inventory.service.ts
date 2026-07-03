import { Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { InventoryService } from '../inventory/inventory.service';
import {
  InventoryLedgerEntry,
  InventoryLedgerDocument,
} from '../inventory/schemas/inventory-ledger.schema';
import { StoreInvoice, StoreInvoiceDocument } from './schemas/store-invoice.schema';
import { StoreSaleReturn, StoreSaleReturnDocument } from './schemas/store-sale-return.schema';
import { StoreSyncEventMeta } from './store-sales-sync.service';
import {
  parseExchangeStockLines,
  parseInvoiceStockLines,
  parseReturnStockLines,
  STORE_INVOICE_POSTED,
  STORE_SALE_EXCHANGE_POSTED,
  STORE_SALE_RETURN_POSTED,
  StockLine,
} from './store-sales-inventory.util';

export type StoreSalesInventoryBackfillResult = {
  invoicesProcessed: number;
  returnsProcessed: number;
  ledgerEntriesCreated: number;
  skipped: number;
  dryRun: boolean;
};

@Injectable()
export class StoreSalesInventoryService {
  constructor(
    @InjectModel(InventoryLedgerEntry.name) private readonly ledgerModel: Model<InventoryLedgerDocument>,
    @InjectModel(StoreInvoice.name) private readonly invoiceModel: Model<StoreInvoiceDocument>,
    @InjectModel(StoreSaleReturn.name) private readonly returnModel: Model<StoreSaleReturnDocument>,
    private readonly inventoryService: InventoryService,
  ) {}

  async hasLedgerForEvent(eventId: string, sourceType: string): Promise<boolean> {
    return !!(await this.ledgerModel.exists({ sourceId: eventId, sourceType }).lean());
  }

  async postInvoiceLedger(meta: StoreSyncEventMeta, payload: Record<string, unknown>): Promise<number> {
    if (await this.hasLedgerForEvent(meta.eventId, STORE_INVOICE_POSTED)) return 0;

    const lines = parseInvoiceStockLines(payload);
    if (lines.length === 0) return 0;

    const note =
      (typeof payload.billNo === 'string' && payload.billNo.trim()) ||
      (typeof payload.invoiceNo === 'string' && payload.invoiceNo.trim()) ||
      meta.eventId;

    return await this.postStoreLedger(meta.storeId, meta.eventId, STORE_INVOICE_POSTED, note, lines, -1);
  }

  async postReturnLedger(
    meta: StoreSyncEventMeta,
    payload: Record<string, unknown>,
    kind: 'return' | 'exchange',
  ): Promise<number> {
    const returnNo =
      (typeof payload.returnNo === 'string' && payload.returnNo.trim()) || meta.eventId;
    let created = 0;

    if (!(await this.hasLedgerForEvent(meta.eventId, STORE_SALE_RETURN_POSTED))) {
      const returnLines = parseReturnStockLines(payload);
      if (returnLines.length > 0) {
        created += await this.postStoreLedger(
          meta.storeId,
          meta.eventId,
          STORE_SALE_RETURN_POSTED,
          returnNo,
          returnLines,
          1,
        );
      }
    }

    if (kind === 'exchange' && !(await this.hasLedgerForEvent(meta.eventId, STORE_SALE_EXCHANGE_POSTED))) {
      const exchangeLines = parseExchangeStockLines(payload);
      if (exchangeLines.length > 0) {
        created += await this.postStoreLedger(
          meta.storeId,
          meta.eventId,
          STORE_SALE_EXCHANGE_POSTED,
          returnNo,
          exchangeLines,
          -1,
        );
      }
    }

    return created;
  }

  private async postStoreLedger(
    storeId: string,
    sourceId: string,
    sourceType: string,
    note: string,
    lines: StockLine[],
    sign: 1 | -1,
  ): Promise<number> {
    const entries = lines.map((l) => ({
      sku: l.sku,
      qtyDelta: sign * l.qty,
      sourceType,
      sourceId,
      note,
      locationKind: 'store' as const,
      storeId,
    }));

    await this.inventoryService.addLedgerEntries(entries);
    return entries.length;
  }

  async backfillAll(options?: { dryRun?: boolean; batchSize?: number }): Promise<StoreSalesInventoryBackfillResult> {
    const dryRun = options?.dryRun === true;
    const batchSize = Math.max(1, Math.min(500, options?.batchSize ?? 100));

    const result: StoreSalesInventoryBackfillResult = {
      invoicesProcessed: 0,
      returnsProcessed: 0,
      ledgerEntriesCreated: 0,
      skipped: 0,
      dryRun,
    };

    let invoiceSkip = 0;
    while (true) {
      const batch = await this.invoiceModel
        .find()
        .sort({ createdAt: 1, _id: 1 })
        .skip(invoiceSkip)
        .limit(batchSize)
        .lean();

      if (batch.length === 0) break;

      for (const doc of batch) {
        result.invoicesProcessed++;
        const meta: StoreSyncEventMeta = {
          eventId: doc.sourceEventId,
          storeId: doc.storeId,
          deviceId: doc.deviceId,
        };
        const payload = doc.payload as Record<string, unknown>;

        const alreadyPosted = await this.hasLedgerForEvent(meta.eventId, STORE_INVOICE_POSTED);
        const lines = parseInvoiceStockLines(payload);
        if (alreadyPosted || lines.length === 0) {
          result.skipped++;
          continue;
        }

        if (!dryRun) {
          result.ledgerEntriesCreated += await this.postInvoiceLedger(meta, payload);
        } else {
          result.ledgerEntriesCreated += lines.length;
        }
      }

      invoiceSkip += batch.length;
      if (batch.length < batchSize) break;
    }

    let returnSkip = 0;
    while (true) {
      const batch = await this.returnModel
        .find()
        .sort({ createdAt: 1, _id: 1 })
        .skip(returnSkip)
        .limit(batchSize)
        .lean();

      if (batch.length === 0) break;

      for (const doc of batch) {
        result.returnsProcessed++;
        const meta: StoreSyncEventMeta = {
          eventId: doc.sourceEventId,
          storeId: doc.storeId,
          deviceId: doc.deviceId,
        };
        const payload = doc.payload as Record<string, unknown>;
        const kind = doc.kind === 'exchange' ? 'exchange' : 'return';

        const returnPosted = await this.hasLedgerForEvent(meta.eventId, STORE_SALE_RETURN_POSTED);
        const exchangePosted =
          kind === 'exchange'
            ? await this.hasLedgerForEvent(meta.eventId, STORE_SALE_EXCHANGE_POSTED)
            : true;
        const returnLines = parseReturnStockLines(payload);
        const exchangeLines = kind === 'exchange' ? parseExchangeStockLines(payload) : [];

        const nothingToDo =
          (returnPosted || returnLines.length === 0) &&
          (exchangePosted || exchangeLines.length === 0);

        if (nothingToDo) {
          result.skipped++;
          continue;
        }

        if (!dryRun) {
          result.ledgerEntriesCreated += await this.postReturnLedger(meta, payload, kind);
        } else {
          if (!returnPosted && returnLines.length > 0) result.ledgerEntriesCreated += returnLines.length;
          if (kind === 'exchange' && !exchangePosted && exchangeLines.length > 0) {
            result.ledgerEntriesCreated += exchangeLines.length;
          }
        }
      }

      returnSkip += batch.length;
      if (batch.length < batchSize) break;
    }

    return result;
  }
}
