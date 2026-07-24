import {
  BadRequestException,
  Injectable,
  UnauthorizedException,
} from '@nestjs/common';
import { JwtService } from '@nestjs/jwt';
import { InjectModel } from '@nestjs/mongoose';
import * as bcrypt from 'bcryptjs';
import { randomUUID } from 'crypto';
import { Model, Types } from 'mongoose';
import { JwtPayload } from '../../common/jwt-payload';
import { BarcodeLabelDesignService } from '../barcode-label-designs/barcode-label-design.service';
import { BillsService } from '../bills/bills.service';
import { Brand, BrandDocument } from '../brands/schemas/brand.schema';
import { Category, CategoryDocument } from '../categories/schemas/category.schema';
import { CustomersService } from '../customers/customers.service';
import { CreateCustomerDto } from '../customers/dto/create-customer.dto';
import { UpdateCustomerDto } from '../customers/dto/update-customer.dto';
import { InventoryAdjustmentsService } from '../inventory-adjustments/inventory-adjustments.service';
import { InventoryService } from '../inventory/inventory.service';
import { Product, ProductDocument } from '../products/schemas/product.schema';
import { SalesmenService } from '../salesmen/salesmen.service';
import { CreateSalesmanDto } from '../salesmen/dto/create-salesman.dto';
import { UpdateSalesmanDto } from '../salesmen/dto/update-salesman.dto';
import { StoreCashMovement, StoreCashMovementDocument } from '../store-sales/schemas/store-cash-movement.schema';
import { StoreCreditNote, StoreCreditNoteDocument } from '../store-sales/schemas/store-credit-note.schema';
import { StoreDailyExpense, StoreDailyExpenseDocument } from '../store-sales/schemas/store-daily-expense.schema';
import { StoreDayClose, StoreDayCloseDocument } from '../store-sales/schemas/store-day-close.schema';
import { StoreInvoice, StoreInvoiceDocument } from '../store-sales/schemas/store-invoice.schema';
import { StoreQuotation, StoreQuotationDocument } from '../store-sales/schemas/store-quotation.schema';
import { StoreSaleReturn, StoreSaleReturnDocument } from '../store-sales/schemas/store-sale-return.schema';
import { StoreSalesSyncService } from '../store-sales/store-sales-sync.service';
import { StoresService } from '../stores/stores.service';
import { UsersService } from '../users/users.service';
import { WhatsAppInvoiceService } from '../whatsapp/whatsapp-invoice.service';
import {
  CompanyBillingSettings,
  CompanyBillingSettingsDocument,
} from './schemas/company-billing-settings.schema';

type SessionUser = {
  id: string;
  email: string;
  name?: string;
  role: string;
  storeId: string;
  deviceId: string;
  posCounter: string;
  isPrimaryTill: boolean;
};

@Injectable()
export class PosV2Service {
  constructor(
    private readonly jwtService: JwtService,
    private readonly usersService: UsersService,
    private readonly storesService: StoresService,
    private readonly inventoryService: InventoryService,
    private readonly billsService: BillsService,
    private readonly storeSalesSync: StoreSalesSyncService,
    private readonly customersService: CustomersService,
    private readonly salesmenService: SalesmenService,
    private readonly inventoryAdjustmentsService: InventoryAdjustmentsService,
    private readonly barcodeLabelDesignService: BarcodeLabelDesignService,
    private readonly whatsAppInvoiceService: WhatsAppInvoiceService,
    @InjectModel(CompanyBillingSettings.name)
    private readonly settingsModel: Model<CompanyBillingSettingsDocument>,
    @InjectModel(Product.name) private readonly productModel: Model<ProductDocument>,
    @InjectModel(Category.name) private readonly categoryModel: Model<CategoryDocument>,
    @InjectModel(Brand.name) private readonly brandModel: Model<BrandDocument>,
    @InjectModel(StoreInvoice.name) private readonly invoiceModel: Model<StoreInvoiceDocument>,
    @InjectModel(StoreQuotation.name) private readonly quotationModel: Model<StoreQuotationDocument>,
    @InjectModel(StoreSaleReturn.name) private readonly returnModel: Model<StoreSaleReturnDocument>,
    @InjectModel(StoreDayClose.name) private readonly dayCloseModel: Model<StoreDayCloseDocument>,
    @InjectModel(StoreCashMovement.name)
    private readonly cashMovementModel: Model<StoreCashMovementDocument>,
    @InjectModel(StoreDailyExpense.name)
    private readonly expenseModel: Model<StoreDailyExpenseDocument>,
    @InjectModel(StoreCreditNote.name)
    private readonly creditNoteModel: Model<StoreCreditNoteDocument>,
  ) {}

  health() {
    return { ok: true, surface: 'pos-v2', version: '1.0.0' };
  }

  private meta(storeId: string, deviceId: string, eventId?: string) {
    return {
      eventId: eventId ?? randomUUID(),
      storeId: storeId.trim(),
      deviceId: deviceId.trim() || 'web-pos',
    };
  }

  private requireStoreId(storeId?: string) {
    const sid = (storeId ?? '').trim();
    if (!sid) throw new BadRequestException('storeId is required');
    return sid;
  }

  async login(body: {
    email: string;
    password: string;
    storeId?: string;
    deviceId?: string;
    posCounter?: string;
  }) {
    const email = (body.email ?? '').trim().toLowerCase();
    const password = body.password ?? '';
    if (!email || !password) throw new BadRequestException('email and password are required');

    const user = await this.usersService.findForLogin(email);
    if (!user || user.status !== 'active') {
      throw new UnauthorizedException('Invalid credentials or account is not active');
    }
    const hash = (user as { passwordHash?: string }).passwordHash;
    if (!hash || !(await bcrypt.compare(password, hash))) {
      throw new UnauthorizedException('Invalid credentials');
    }

    const storeId = (body.storeId?.trim() || user.storeId || '').trim();
    if (!storeId) throw new BadRequestException('storeId is required for POS login');
    if (user.role === 'store' && user.storeId && user.storeId !== storeId) {
      throw new UnauthorizedException('User is not assigned to this store');
    }
    const exists = await this.storesService.existsByCode(storeId);
    if (!exists) throw new BadRequestException(`Unknown storeId '${storeId}'`);

    const deviceId = (body.deviceId?.trim() || `web-${randomUUID().slice(0, 8)}`).trim();
    const posCounter = (body.posCounter?.trim() || '1').trim();

    const payload: JwtPayload = {
      sub: String(user._id),
      email: user.email,
      role: user.role,
      locationKind: user.locationKind ?? 'store',
      storeId,
      deviceId,
      posCounter,
    };
    const accessToken = this.jwtService.sign(payload);
    const session = await this.buildSession(payload, user as Record<string, unknown>);
    return { accessToken, session };
  }

  async getSession(jwtUser: JwtPayload) {
    if (!jwtUser?.sub) throw new UnauthorizedException();
    const user = await this.usersService.findById(jwtUser.sub);
    return this.buildSession(jwtUser, user as unknown as Record<string, unknown>);
  }

  private async buildSession(
    jwtUser: JwtPayload,
    user: Record<string, unknown>,
  ): Promise<{ user: SessionUser; store: Record<string, unknown> | null }> {
    const storeId = this.requireStoreId(jwtUser.storeId);
    const posCounter = jwtUser.posCounter ?? '1';
    const deviceId = jwtUser.deviceId ?? 'web-pos';
    let store: Record<string, unknown> | null = null;
    try {
      const s = await this.storesService.findByCode(storeId);
      store = s as unknown as Record<string, unknown>;
    } catch {
      store = { code: storeId };
    }
    const sessionUser: SessionUser = {
      id: String(user._id ?? jwtUser.sub),
      email: String(user.email ?? jwtUser.email),
      role: String(user.role ?? jwtUser.role),
      storeId,
      deviceId,
      posCounter,
      isPrimaryTill: posCounter === '1',
    };
    if (typeof user.name === 'string') sessionUser.name = user.name;
    return { user: sessionUser, store };
  }

  async getBillingSettings(storeId: string) {
    const sid = this.requireStoreId(storeId).toLowerCase();
    let doc = await this.settingsModel.findOne({ storeId: sid }).lean();
    if (!doc) {
      await this.settingsModel.create({
        storeId: sid,
        mode: 'retail',
        allowPerBillSwitch: false,
      });
      doc = await this.settingsModel.findOne({ storeId: sid }).lean();
    }
    return {
      storeId: doc!.storeId,
      mode: doc!.mode,
      allowPerBillSwitch: doc!.allowPerBillSwitch,
    };
  }

  async patchBillingSettings(
    storeId: string,
    body: { mode?: string; allowPerBillSwitch?: boolean },
  ) {
    const sid = this.requireStoreId(storeId).toLowerCase();
    const mode = body.mode?.trim().toLowerCase() === 'wholesale' ? 'wholesale' : 'retail';
    const allowPerBillSwitch = Boolean(body.allowPerBillSwitch);
    const doc = await this.settingsModel
      .findOneAndUpdate(
        { storeId: sid },
        { $set: { mode, allowPerBillSwitch } },
        { upsert: true, new: true },
      )
      .lean();
    return {
      storeId: doc!.storeId,
      mode: doc!.mode,
      allowPerBillSwitch: doc!.allowPerBillSwitch,
    };
  }

  async getCatalog(params: {
    storeId: string;
    search?: string;
    category?: string;
    limit?: number;
  }) {
    const storeId = this.requireStoreId(params.storeId);
    const limit = Math.min(Math.max(params.limit ?? 200, 1), 500);
    const filter: Record<string, unknown> = { isActive: { $ne: false } };
    if (params.search?.trim()) {
      const s = params.search.trim();
      filter.$or = [
        { itemName: { $regex: s, $options: 'i' } },
        { sku: { $regex: s, $options: 'i' } },
        { upcEanCode: s },
      ];
    }

    const products = await this.productModel.find(filter).limit(limit).lean();
    const categoryIds = [
      ...new Set(
        products
          .map((p) => p.categoryId)
          .filter((id): id is Types.ObjectId => Boolean(id))
          .map((id) => String(id)),
      ),
    ];
    const brandIds = [
      ...new Set(
        products
          .map((p) => p.brandId)
          .filter((id): id is Types.ObjectId => Boolean(id))
          .map((id) => String(id)),
      ),
    ];

    const [categories, brands, stockMap] = await Promise.all([
      this.categoryModel.find({ _id: { $in: categoryIds } }).lean(),
      this.brandModel.find({ _id: { $in: brandIds } }).lean(),
      this.inventoryService.getStoreQtyBySkus(
        storeId,
        products.map((p) => p.sku),
      ),
    ]);

    const catById = new Map(categories.map((c) => [String(c._id), c.name]));
    const brandById = new Map(brands.map((b) => [String(b._id), b.name]));

    let items = products.map((p) => {
      const retail =
        typeof p.storePrice === 'number' && p.storePrice > 0
          ? p.storePrice
          : typeof p.sellingPrice === 'number'
            ? p.sellingPrice
            : 0;
      const wholesale =
        typeof p.wholesalePrice === 'number' ? p.wholesalePrice : 0;
      const categoryName = p.categoryId ? catById.get(String(p.categoryId)) : undefined;
      const media = Array.isArray(p.mediaItems) ? p.mediaItems[0] : undefined;
      const imageUrl =
        media && typeof (media as { url?: string }).url === 'string'
          ? (media as { url: string }).url
          : null;
      return {
        id: String(p._id),
        sku: p.sku,
        name: p.itemName,
        barcode: p.upcEanCode ?? null,
        category: categoryName ?? null,
        brand: p.brandId ? brandById.get(String(p.brandId)) ?? null : null,
        retailPrice: retail,
        wholesalePrice: wholesale,
        mrp: typeof p.mrp === 'number' ? p.mrp : retail,
        costPrice: typeof p.costPrice === 'number' ? p.costPrice : 0,
        stockQty: stockMap.get(p.sku) ?? 0,
        gstPercent: typeof p.gstPercent === 'number' ? p.gstPercent : 12,
        hsn: p.gstCode ?? null,
        minStock: typeof p.minStock === 'number' ? p.minStock : 5,
        imageUrl,
      };
    });

    if (params.category?.trim() && params.category !== 'All Categories') {
      const cat = params.category.trim().toLowerCase();
      items = items.filter((i) => (i.category ?? '').toLowerCase() === cat);
    }

    const categoryNames = [
      ...new Set(items.map((i) => i.category).filter((c): c is string => Boolean(c))),
    ].sort();

    return { items, categories: categoryNames };
  }

  async getInventory(params: {
    storeId: string;
    search?: string;
    category?: string;
    brand?: string;
    stockLevel?: string;
    page?: number;
    limit?: number;
  }) {
    const page = Math.max(params.page ?? 1, 1);
    const limit = Math.min(Math.max(params.limit ?? 10, 1), 100);
    const catalogParams: {
      storeId: string;
      search?: string;
      category?: string;
      limit: number;
    } = { storeId: params.storeId, limit: 500 };
    if (params.search !== undefined) catalogParams.search = params.search;
    if (params.category !== undefined) catalogParams.category = params.category;
    const catalog = await this.getCatalog(catalogParams);

    let items = catalog.items;
    if (params.brand?.trim() && params.brand !== 'All Brands') {
      const b = params.brand.trim().toLowerCase();
      items = items.filter((i) => (i.brand ?? '').toLowerCase() === b);
    }
    if (params.stockLevel === 'low') {
      items = items.filter((i) => i.stockQty > 0 && i.stockQty <= (i.minStock || 5));
    } else if (params.stockLevel === 'out') {
      items = items.filter((i) => i.stockQty <= 0);
    }

    const total = items.length;
    const slice = items.slice((page - 1) * limit, page * limit);
    const lowStock = catalog.items.filter(
      (i) => i.stockQty > 0 && i.stockQty <= (i.minStock || 5),
    ).length;
    const categories = new Set(catalog.items.map((i) => i.category).filter(Boolean)).size;
    const brands = [
      ...new Set(catalog.items.map((i) => i.brand).filter((b): b is string => Boolean(b))),
    ].sort();
    const totalValue = catalog.items.reduce((sum, i) => sum + i.stockQty * i.retailPrice, 0);

    return {
      items: slice,
      page,
      limit,
      total,
      brands,
      categories: catalog.categories,
      kpis: {
        totalItems: catalog.items.length,
        lowStock,
        categories,
        totalValue: Math.round(totalValue * 100) / 100,
      },
    };
  }

  async allocateBillNo(storeId: string) {
    const sid = this.requireStoreId(storeId);
    const day = new Date().toISOString().slice(0, 10).replace(/-/g, '');
    const prefix = `TB-${sid.toUpperCase()}-${day}-`;
    const count = await this.invoiceModel.countDocuments({
      storeId: sid,
      invoiceNo: { $regex: `^${prefix.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}` },
    });
    return `${prefix}${String(count + 1).padStart(4, '0')}`;
  }

  async createBill(
    jwtUser: JwtPayload,
    body: Record<string, unknown>,
  ) {
    const storeId = this.requireStoreId(jwtUser.storeId ?? (body.storeId as string));
    const deviceId = jwtUser.deviceId ?? String(body.deviceId ?? 'web-pos');
    const posCounter = jwtUser.posCounter ?? String(body.posCounter ?? '1');
    const billNo =
      (typeof body.billNo === 'string' && body.billNo.trim()) ||
      (typeof body.invoiceNo === 'string' && body.invoiceNo.trim()) ||
      (await this.allocateBillNo(storeId));

    const meta = this.meta(storeId, deviceId);
    const payload: Record<string, unknown> = {
      ...body,
      billNo,
      invoiceNo: billNo,
      storeId,
      deviceId,
      posCounter,
      billDate: body.billDate ?? new Date().toISOString(),
      status: body.status ?? 'posted',
    };
    await this.storeSalesSync.applyInvoiceCreated(meta, payload);
    return { billNo, eventId: meta.eventId, payload };
  }

  async listBills(query: {
    storeCode: string;
    search?: string;
    from?: string;
    to?: string;
    page?: number;
    limit?: number;
    status?: string;
    paymentMode?: string;
    salesmanCode?: string;
  }) {
    const options: {
      storeCode: string;
      search?: string;
      from?: string;
      to?: string;
      page: number;
      limit: number;
      status?: never;
      paymentMode?: never;
      salesmanCode?: string;
    } = {
      storeCode: query.storeCode,
      page: query.page ?? 1,
      limit: query.limit ?? 20,
    };
    if (query.search !== undefined) options.search = query.search;
    if (query.from !== undefined) options.from = query.from;
    if (query.to !== undefined) options.to = query.to;
    if (query.salesmanCode !== undefined) options.salesmanCode = query.salesmanCode;
    if (query.status !== undefined) options.status = query.status as never;
    if (query.paymentMode !== undefined) options.paymentMode = query.paymentMode as never;
    return this.billsService.listBills(options);
  }

  async getBill(storeCode: string, billNo: string) {
    return this.billsService.getBillDetail(storeCode, billNo);
  }

  async createReturn(jwtUser: JwtPayload, body: Record<string, unknown>, kind: 'return' | 'exchange') {
    const storeId = this.requireStoreId(jwtUser.storeId ?? (body.storeId as string));
    const deviceId = jwtUser.deviceId ?? String(body.deviceId ?? 'web-pos');
    const returnNo =
      (typeof body.returnNo === 'string' && body.returnNo.trim()) ||
      `RT-${Date.now()}`;
    const meta = this.meta(storeId, deviceId);
    const payload = { ...body, returnNo, storeId, deviceId };
    await this.storeSalesSync.applySaleReturn(meta, payload, kind);
    return { returnNo, eventId: meta.eventId };
  }

  async createAdjustment(jwtUser: JwtPayload, body: Record<string, unknown>) {
    const storeId = this.requireStoreId(jwtUser.storeId ?? (body.storeId as string));
    const deviceId = jwtUser.deviceId ?? String(body.deviceId ?? 'web-pos');
    const adjustmentNo =
      (typeof body.adjustmentNo === 'string' && body.adjustmentNo.trim()) ||
      `ADJ-${Date.now()}`;
    const meta = this.meta(storeId, deviceId);
    await this.storeSalesSync.applyAdjustmentCreated(meta, { ...body, adjustmentNo, storeId });
    return { adjustmentNo, eventId: meta.eventId };
  }

  async upsertQuotation(jwtUser: JwtPayload, body: Record<string, unknown>) {
    const storeId = this.requireStoreId(jwtUser.storeId ?? (body.storeId as string));
    const deviceId = jwtUser.deviceId ?? String(body.deviceId ?? 'web-pos');
    const quotationNo =
      (typeof body.quotationNo === 'string' && body.quotationNo.trim()) ||
      `Q-${Date.now()}`;
    const meta = this.meta(storeId, deviceId);
    await this.storeSalesSync.applyQuotationUpserted(meta, {
      ...body,
      quotationNo,
      storeId,
      status: body.status ?? 'open',
    });
    return { quotationNo, eventId: meta.eventId };
  }

  async convertQuotation(jwtUser: JwtPayload, body: Record<string, unknown>) {
    const storeId = this.requireStoreId(jwtUser.storeId ?? (body.storeId as string));
    const deviceId = jwtUser.deviceId ?? String(body.deviceId ?? 'web-pos');
    const meta = this.meta(storeId, deviceId);
    await this.storeSalesSync.applyQuotationConverted(meta, body);
    return { eventId: meta.eventId };
  }

  async cancelQuotation(jwtUser: JwtPayload, body: Record<string, unknown>) {
    const storeId = this.requireStoreId(jwtUser.storeId ?? (body.storeId as string));
    const deviceId = jwtUser.deviceId ?? String(body.deviceId ?? 'web-pos');
    const meta = this.meta(storeId, deviceId);
    await this.storeSalesSync.applyQuotationCancelled(meta, body);
    return { eventId: meta.eventId };
  }

  async listQuotations(storeId: string, search?: string) {
    const sid = this.requireStoreId(storeId);
    const filter: Record<string, unknown> = { storeId: sid };
    if (search?.trim()) {
      filter.$or = [
        { quotationNo: { $regex: search.trim(), $options: 'i' } },
        { 'payload.customerName': { $regex: search.trim(), $options: 'i' } },
      ];
    }
    const rows = await this.quotationModel.find(filter).sort({ createdAt: -1 }).limit(100).lean();
    return {
      items: rows.map((r) => ({
        quotationNo: r.quotationNo,
        status: r.status,
        convertedBillNo: r.convertedBillNo,
        payload: r.payload,
        createdAt: (r as { createdAt?: Date }).createdAt,
      })),
    };
  }

  async createCreditNote(jwtUser: JwtPayload, body: Record<string, unknown>) {
    const storeId = this.requireStoreId(jwtUser.storeId ?? (body.storeId as string));
    const deviceId = jwtUser.deviceId ?? String(body.deviceId ?? 'web-pos');
    const meta = this.meta(storeId, deviceId);
    await this.storeSalesSync.applyCreditNoteCreated(meta, body);
    return { eventId: meta.eventId };
  }

  async applyCreditNote(jwtUser: JwtPayload, body: Record<string, unknown>) {
    const storeId = this.requireStoreId(jwtUser.storeId ?? (body.storeId as string));
    const deviceId = jwtUser.deviceId ?? String(body.deviceId ?? 'web-pos');
    const meta = this.meta(storeId, deviceId);
    await this.storeSalesSync.applyCreditNoteApplied(meta, body);
    return { eventId: meta.eventId };
  }

  async cashoutCreditNote(jwtUser: JwtPayload, body: Record<string, unknown>) {
    const storeId = this.requireStoreId(jwtUser.storeId ?? (body.storeId as string));
    const deviceId = jwtUser.deviceId ?? String(body.deviceId ?? 'web-pos');
    const meta = this.meta(storeId, deviceId);
    await this.storeSalesSync.applyCreditNoteCashedOut(meta, body);
    return { eventId: meta.eventId };
  }

  async recordCodPayment(jwtUser: JwtPayload, body: Record<string, unknown>) {
    const storeId = this.requireStoreId(jwtUser.storeId ?? (body.storeId as string));
    const deviceId = jwtUser.deviceId ?? String(body.deviceId ?? 'web-pos');
    const meta = this.meta(storeId, deviceId);
    await this.storeSalesSync.applyInvoiceCodPaymentReceived(meta, body);
    return { eventId: meta.eventId };
  }

  async recordCreditPayment(jwtUser: JwtPayload, body: Record<string, unknown>) {
    const storeId = this.requireStoreId(jwtUser.storeId ?? (body.storeId as string));
    const deviceId = jwtUser.deviceId ?? String(body.deviceId ?? 'web-pos');
    const meta = this.meta(storeId, deviceId);
    await this.storeSalesSync.applyInvoiceCreditPaymentReceived(meta, body);
    return { eventId: meta.eventId };
  }

  async openDaySession(jwtUser: JwtPayload, body: Record<string, unknown>) {
    const storeId = this.requireStoreId(jwtUser.storeId ?? (body.storeId as string));
    const deviceId = jwtUser.deviceId ?? String(body.deviceId ?? 'web-pos');
    const posCounter = jwtUser.posCounter ?? String(body.posCounter ?? '1');
    const businessDate =
      (typeof body.businessDate === 'string' && body.businessDate.trim()) ||
      new Date().toISOString().slice(0, 10);
    const meta = this.meta(storeId, deviceId);
    await this.storeSalesSync.applyDaySessionOpened(meta, {
      ...body,
      businessDate,
      posCounter,
      storeId,
      status: body.status ?? 'open',
    });
    return { eventId: meta.eventId, businessDate, posCounter };
  }

  async closeDaySession(jwtUser: JwtPayload, body: Record<string, unknown>) {
    const storeId = this.requireStoreId(jwtUser.storeId ?? (body.storeId as string));
    const deviceId = jwtUser.deviceId ?? String(body.deviceId ?? 'web-pos');
    const posCounter = jwtUser.posCounter ?? String(body.posCounter ?? '1');
    const businessDate =
      (typeof body.businessDate === 'string' && body.businessDate.trim()) ||
      new Date().toISOString().slice(0, 10);
    const meta = this.meta(storeId, deviceId);
    await this.storeSalesSync.applyDaySessionClosed(meta, {
      ...body,
      businessDate,
      posCounter,
      storeId,
      status: 'closed',
    });
    return { eventId: meta.eventId };
  }

  async createCashMovement(jwtUser: JwtPayload, body: Record<string, unknown>) {
    const storeId = this.requireStoreId(jwtUser.storeId ?? (body.storeId as string));
    const deviceId = jwtUser.deviceId ?? String(body.deviceId ?? 'web-pos');
    const meta = this.meta(storeId, deviceId);
    await this.storeSalesSync.applyCashMovementCreated(meta, { ...body, storeId });
    return { eventId: meta.eventId };
  }

  async createExpense(jwtUser: JwtPayload, body: Record<string, unknown>) {
    const storeId = this.requireStoreId(jwtUser.storeId ?? (body.storeId as string));
    const deviceId = jwtUser.deviceId ?? String(body.deviceId ?? 'web-pos');
    const meta = this.meta(storeId, deviceId);
    await this.storeSalesSync.applyDailyExpenseCreated(meta, { ...body, storeId });
    return { eventId: meta.eventId };
  }

  async listDaySessions(storeId: string, from?: string, to?: string) {
    const sid = this.requireStoreId(storeId);
    const filter: Record<string, unknown> = { storeId: sid };
    if (from || to) {
      filter.businessDate = {};
      if (from) (filter.businessDate as Record<string, string>).$gte = from;
      if (to) (filter.businessDate as Record<string, string>).$lte = to;
    }
    const rows = await this.dayCloseModel.find(filter).sort({ businessDate: -1 }).limit(60).lean();
    return { items: rows };
  }

  async listExpenses(storeId: string, limit = 50) {
    const sid = this.requireStoreId(storeId);
    const rows = await this.expenseModel.find({ storeId: sid }).sort({ createdAt: -1 }).limit(limit).lean();
    return { items: rows };
  }

  async listCashMovements(storeId: string, limit = 50) {
    const sid = this.requireStoreId(storeId);
    const rows = await this.cashMovementModel
      .find({ storeId: sid })
      .sort({ createdAt: -1 })
      .limit(limit)
      .lean();
    return { items: rows };
  }

  async listOnlineSales(storeId: string) {
    const sid = this.requireStoreId(storeId);
    const rows = await this.invoiceModel
      .find({
        storeId: sid,
        $or: [
          { 'payload.isOnlineCod': true },
          { 'payload.onlineCod': true },
          { 'payload.flags.onlineCod': true },
        ],
      })
      .sort({ createdAt: -1 })
      .limit(100)
      .lean();
    return {
      items: rows.map((r) => ({
        billNo: r.invoiceNo,
        payload: r.payload,
        createdAt: (r as { createdAt?: Date }).createdAt,
      })),
    };
  }

  async listCreditBills(storeId: string, status?: string) {
    const sid = this.requireStoreId(storeId);
    const rows = await this.invoiceModel
      .find({
        storeId: sid,
        $or: [
          { 'payload.isCredit': true },
          { 'payload.creditSale': true },
          { 'payload.paymentMode': 'credit' },
          { 'payload.flags.credit': true },
        ],
      })
      .sort({ createdAt: -1 })
      .limit(100)
      .lean();
    let items = rows.map((r) => ({
      billNo: r.invoiceNo,
      payload: r.payload,
      createdAt: (r as { createdAt?: Date }).createdAt,
      balance:
        Number((r.payload as { creditBalance?: number }).creditBalance ?? 0) ||
        Number((r.payload as { payable?: number }).payable ?? 0),
    }));
    if (status === 'pending') items = items.filter((i) => i.balance > 0);
    if (status === 'settled') items = items.filter((i) => i.balance <= 0);
    return { items };
  }

  async listReturns(storeId: string) {
    const sid = this.requireStoreId(storeId);
    const rows = await this.returnModel.find({ storeId: sid }).sort({ createdAt: -1 }).limit(100).lean();
    return { items: rows };
  }

  async listCreditNotes(storeId: string) {
    const sid = this.requireStoreId(storeId);
    const rows = await this.creditNoteModel.find({ storeId: sid }).sort({ createdAt: -1 }).limit(100).lean();
    return { items: rows };
  }

  async dashboardOverview(storeId: string) {
    const sid = this.requireStoreId(storeId);
    const since = new Date();
    since.setDate(since.getDate() - 14);
    const invoices = await this.invoiceModel
      .find({ storeId: sid, createdAt: { $gte: since } })
      .sort({ createdAt: -1 })
      .limit(500)
      .lean();
    const salesTotal = invoices.reduce((sum, inv) => {
      const p = inv.payload as { payable?: number; grandTotal?: number };
      return sum + Number(p.payable ?? p.grandTotal ?? 0);
    }, 0);
    const byDay = new Map<string, number>();
    for (const inv of invoices) {
      const d = ((inv as { createdAt?: Date }).createdAt ?? new Date()).toISOString().slice(0, 10);
      const p = inv.payload as { payable?: number; grandTotal?: number };
      byDay.set(d, (byDay.get(d) ?? 0) + Number(p.payable ?? p.grandTotal ?? 0));
    }
    const trend = [...byDay.entries()]
      .sort((a, b) => a[0].localeCompare(b[0]))
      .map(([date, total]) => ({ date, total }));
    return {
      invoiceCount: invoices.length,
      salesTotal: Math.round(salesTotal * 100) / 100,
      recentBills: invoices.slice(0, 10).map((i) => ({
        billNo: i.invoiceNo,
        payable: Number((i.payload as { payable?: number }).payable ?? 0),
        createdAt: (i as { createdAt?: Date }).createdAt,
      })),
      trend,
    };
  }

  async analytics(storeId: string) {
    return this.dashboardOverview(storeId);
  }

  async listCustomers(search?: string, phone?: string) {
    const params: { search?: string; phone?: string } = {};
    if (search !== undefined) params.search = search;
    if (phone !== undefined) params.phone = phone;
    return this.customersService.list(params);
  }

  async createCustomer(dto: CreateCustomerDto) {
    return this.customersService.create(dto);
  }

  async updateCustomer(id: string, dto: UpdateCustomerDto) {
    return this.customersService.update(id, dto);
  }

  async listSalesmen(storeId: string, search?: string) {
    return this.salesmenService.listByStore(this.requireStoreId(storeId), search);
  }

  async createSalesman(dto: CreateSalesmanDto) {
    return this.salesmenService.create(dto);
  }

  async updateSalesman(id: string, dto: UpdateSalesmanDto) {
    return this.salesmenService.update(id, dto);
  }

  async createInventoryAdjustment(dto: unknown) {
    return this.inventoryAdjustmentsService.createFromAdmin(dto as never);
  }

  async getActiveBarcodeDesign() {
    return this.barcodeLabelDesignService.getActiveDesign();
  }

  async getWhatsAppSettings(storeId?: string) {
    return this.whatsAppInvoiceService.getSettings(storeId);
  }

  async sendWhatsAppInvoice(input: {
    storeId: string;
    billNo: string;
    phone: string;
    customerName?: string;
    payable?: number;
    attachmentBase64?: string;
    attachmentFilename?: string;
  }) {
    if (!input.attachmentBase64?.trim()) {
      throw new BadRequestException('attachmentBase64 is required to send WhatsApp invoice');
    }
    const attachment = Buffer.from(input.attachmentBase64, 'base64');
    return this.whatsAppInvoiceService.sendInvoice({
      storeId: input.storeId,
      billNo: input.billNo,
      customerPhone: input.phone,
      customerName: input.customerName ?? 'Customer',
      payable: Number(input.payable ?? 0),
      attachment,
      attachmentFilename: input.attachmentFilename ?? `${input.billNo}.pdf`,
    });
  }

  async getMasters(storeId: string) {
    const catalog = await this.getCatalog({ storeId, limit: 500 });
    return {
      categories: catalog.categories,
      brands: [
        ...new Set(catalog.items.map((i) => i.brand).filter((b): b is string => Boolean(b))),
      ].sort(),
    };
  }
}
