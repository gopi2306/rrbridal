import { BadRequestException, Injectable } from '@nestjs/common';
import { createHash, randomUUID } from 'crypto';
import { InventoryService } from '../inventory/inventory.service';
import { ProductsService } from '../products/products.service';
import { StoresService } from '../stores/stores.service';
import { SyncEventDto } from '../sync/dto/sync-push.dto';
import { SyncService } from '../sync/sync.service';

export type StorePosCatalogProduct = {
  centralId: string;
  sku: string;
  upcEanCode: string | null;
  name: string;
  shortName: string | null;
  alias: string | null;
  costPrice: number | null;
  marginPercent: number | null;
  mrp: number | null;
  sellingPrice: number | null;
  storePrice: number | null;
  gstPercent: number | null;
  hsnSac: string | null;
  categoryId: string | null;
  brandId: string | null;
  offerGroupId: string | null;
  stockQty: number;
  mediaItems: Array<{ url: string; description?: string | null }>;
};

@Injectable()
export class StorePosService {
  constructor(
    private readonly syncService: SyncService,
    private readonly productsService: ProductsService,
    private readonly inventoryService: InventoryService,
    private readonly storesService: StoresService,
  ) {}

  async searchCatalog(storeCode: string, q: string, limit = 80): Promise<StorePosCatalogProduct[]> {
    const storeId = storeCode?.trim();
    const query = q?.trim() ?? '';
    if (!storeId) throw new BadRequestException('storeCode is required');
    if (!query) return [];

    const exists = await this.storesService.existsByCode(storeId);
    if (!exists) throw new BadRequestException(`Unknown storeCode '${storeId}'`);

    const take = Math.min(100, Math.max(1, limit));
    const [products, storeQtyMap] = await Promise.all([
      this.productsService.list({
        search: query,
        skip: 0,
        limit: Math.max(take * 3, 120),
      }),
      this.inventoryService.getStoreSkuQtyMap(storeId),
    ]);

    const results: StorePosCatalogProduct[] = [];
    const seen = new Set<string>();

    for (const p of products) {
      const sku = typeof p.sku === 'string' ? p.sku.trim() : '';
      if (!sku || seen.has(sku.toLowerCase())) continue;
      const stockQty = storeQtyMap.get(sku) ?? 0;
      if (stockQty <= 0) continue;

      seen.add(sku.toLowerCase());
      results.push(this.mapProduct(p as Record<string, unknown>, stockQty));
      if (results.length >= take) break;
    }

    return results;
  }

  async applyEvent(input: {
    type: string;
    storeId: string;
    deviceId: string;
    payload: Record<string, unknown>;
    eventId?: string;
    hash?: string;
    createdAt?: string;
  }) {
    const storeId = input.storeId?.trim();
    const deviceId = input.deviceId?.trim();
    const type = input.type?.trim();
    if (!storeId || !deviceId || !type) {
      throw new BadRequestException('storeId, deviceId, and type are required');
    }

    const event: SyncEventDto = {
      eventId: input.eventId?.trim() || randomUUID(),
      storeId,
      deviceId,
      type,
      createdAt: input.createdAt?.trim() || new Date().toISOString(),
      payload: input.payload ?? {},
      hash: input.hash?.trim() || this.hashPayload(input.payload ?? {}),
    };

    const result = await this.syncService.applyOne(event);
    if (result.status === 'rejected') {
      throw new BadRequestException(result.reason || `Event ${type} was rejected`);
    }
    return { ...result, eventId: event.eventId };
  }

  async createBill(storeId: string, deviceId: string, payload: Record<string, unknown>) {
    return this.applyEvent({ type: 'InvoiceCreated', storeId, deviceId, payload });
  }

  async deleteBill(storeId: string, deviceId: string, billNo: string, payload?: Record<string, unknown>) {
    return this.applyEvent({
      type: 'InvoiceDeleted',
      storeId,
      deviceId,
      payload: { ...(payload ?? {}), billNo, invoiceNo: billNo },
    });
  }

  async createSaleReturn(storeId: string, deviceId: string, payload: Record<string, unknown>, exchange = false) {
    return this.applyEvent({
      type: exchange ? 'SaleExchangeCreated' : 'SaleReturnCreated',
      storeId,
      deviceId,
      payload,
    });
  }

  async upsertQuotation(storeId: string, deviceId: string, payload: Record<string, unknown>) {
    return this.applyEvent({ type: 'QuotationUpserted', storeId, deviceId, payload });
  }

  async convertQuotation(storeId: string, deviceId: string, payload: Record<string, unknown>) {
    return this.applyEvent({ type: 'QuotationConverted', storeId, deviceId, payload });
  }

  async cancelQuotation(storeId: string, deviceId: string, payload: Record<string, unknown>) {
    return this.applyEvent({ type: 'QuotationCancelled', storeId, deviceId, payload });
  }

  async createCreditNote(storeId: string, deviceId: string, payload: Record<string, unknown>) {
    return this.applyEvent({ type: 'CreditNoteCreated', storeId, deviceId, payload });
  }

  async applyCreditNote(storeId: string, deviceId: string, payload: Record<string, unknown>) {
    return this.applyEvent({ type: 'CreditNoteApplied', storeId, deviceId, payload });
  }

  async cashoutCreditNote(storeId: string, deviceId: string, payload: Record<string, unknown>) {
    return this.applyEvent({ type: 'CreditNoteCashedOut', storeId, deviceId, payload });
  }

  async openDaySession(storeId: string, deviceId: string, payload: Record<string, unknown>) {
    return this.applyEvent({ type: 'DaySessionOpened', storeId, deviceId, payload });
  }

  async closeDaySession(storeId: string, deviceId: string, payload: Record<string, unknown>) {
    return this.applyEvent({ type: 'DaySessionClosed', storeId, deviceId, payload });
  }

  async createCashMovement(storeId: string, deviceId: string, payload: Record<string, unknown>) {
    return this.applyEvent({ type: 'CashMovementCreated', storeId, deviceId, payload });
  }

  async createDailyExpense(storeId: string, deviceId: string, payload: Record<string, unknown>) {
    return this.applyEvent({ type: 'DailyExpenseCreated', storeId, deviceId, payload });
  }

  async receiveCodPayment(storeId: string, deviceId: string, billNo: string, payload: Record<string, unknown>) {
    return this.applyEvent({
      type: 'InvoiceCodPaymentReceived',
      storeId,
      deviceId,
      payload: { ...payload, billNo },
    });
  }

  async receiveCreditPayment(storeId: string, deviceId: string, billNo: string, payload: Record<string, unknown>) {
    return this.applyEvent({
      type: 'InvoiceCreditPaymentReceived',
      storeId,
      deviceId,
      payload: { ...payload, billNo },
    });
  }

  async createAdjustmentBill(storeId: string, deviceId: string, payload: Record<string, unknown>) {
    return this.applyEvent({ type: 'AdjustmentBillCreated', storeId, deviceId, payload });
  }

  async approveStockExceptions(storeId: string, deviceId: string, billNo: string, payload: Record<string, unknown>) {
    return this.applyEvent({
      type: 'InvoiceStockExceptionsApproved',
      storeId,
      deviceId,
      payload: { ...payload, billNo, invoiceNo: billNo },
    });
  }

  async updateBillWhatsApp(storeId: string, deviceId: string, billNo: string, payload: Record<string, unknown>) {
    return this.applyEvent({
      type: 'InvoiceWhatsAppUpdated',
      storeId,
      deviceId,
      payload: { ...payload, billNo, invoiceNo: billNo },
    });
  }

  async appendBillPrintAudit(storeId: string, deviceId: string, billNo: string, payload: Record<string, unknown>) {
    return this.applyEvent({
      type: 'InvoicePrintAuditAppended',
      storeId,
      deviceId,
      payload: { ...payload, billNo, invoiceNo: billNo },
    });
  }

  async markCashHandOverPrinted(storeId: string, deviceId: string, payload: Record<string, unknown>) {
    return this.applyEvent({ type: 'DaySessionCashHandOverPrinted', storeId, deviceId, payload });
  }

  async recordGatewayPayment(storeId: string, deviceId: string, payload: Record<string, unknown>) {
    return this.applyEvent({ type: 'PaymentRecorded', storeId, deviceId, payload });
  }

  private mapProduct(p: Record<string, unknown>, stockQty: number): StorePosCatalogProduct {
    const idRaw = p._id;
    const centralId =
      typeof idRaw === 'string'
        ? idRaw
        : idRaw && typeof idRaw === 'object' && 'toString' in idRaw
          ? String(idRaw)
          : '';

    const hsn =
      (typeof p.hsnSac === 'string' && p.hsnSac) ||
      (typeof p.hsnCode === 'string' && p.hsnCode) ||
      null;

    const storePrice =
      this.asNumber(p.storePrice) ?? this.asNumber(p.sellingPrice) ?? null;

    return {
      centralId,
      sku: typeof p.sku === 'string' ? p.sku : '',
      upcEanCode: typeof p.upcEanCode === 'string' ? p.upcEanCode : null,
      name: typeof p.itemName === 'string' ? p.itemName : typeof p.sku === 'string' ? p.sku : '',
      shortName: typeof p.shortName === 'string' ? p.shortName : null,
      alias: typeof p.alias === 'string' ? p.alias : null,
      costPrice: this.asNumber(p.costPrice),
      marginPercent: this.asNumber(p.marginPercent),
      mrp: this.asNumber(p.mrp),
      sellingPrice: this.asNumber(p.sellingPrice),
      storePrice,
      gstPercent: this.asNumber(p.gstPercent),
      hsnSac: hsn,
      categoryId: this.asId(p.categoryId),
      brandId: this.asId(p.brandId),
      offerGroupId: this.asId(p.offerGroupId),
      stockQty,
      mediaItems: this.readMedia(p),
    };
  }

  private asId(v: unknown): string | null {
    if (typeof v === 'string' && v.trim()) return v.trim();
    if (v && typeof v === 'object' && 'toString' in v) {
      const s = String(v);
      return s && s !== '[object Object]' ? s : null;
    }
    return null;
  }

  private readMedia(p: Record<string, unknown>): Array<{ url: string; description?: string | null }> {
    const items = p.mediaItems;
    if (!Array.isArray(items)) return [];

    const out: Array<{ url: string; description?: string | null }> = [];
    for (const el of items) {
      if (!el || typeof el !== 'object') continue;
      const row = el as Record<string, unknown>;
      const url = typeof row.url === 'string' ? row.url : '';
      if (!url) continue;
      out.push({
        url,
        description: typeof row.description === 'string' ? row.description : null,
      });
    }
    return out;
  }

  private asNumber(v: unknown): number | null {
    return typeof v === 'number' && Number.isFinite(v) ? v : null;
  }

  private hashPayload(payload: Record<string, unknown>): string {
    return createHash('sha256').update(JSON.stringify(payload)).digest('hex');
  }
}
