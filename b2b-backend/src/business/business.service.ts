import {
  BadRequestException,
  ConflictException,
  Injectable,
  NotFoundException,
} from '@nestjs/common';
import { Collection, Db, Document, MongoServerError, ObjectId } from 'mongodb';
import { requireWriteDatabaseKey } from '../config/database-config';
import { ConnectionRegistryService } from '../database/connection-registry.service';
import {
  CustomerWriteDto,
  DateRangeQueryDto,
  InventoryAdjustmentDto,
  InventoryQueryDto,
  ProductPatchDto,
  PurchaseOrderStatusDto,
  PurchaseOrderWriteDto,
  ScopeQueryDto,
  StoreWriteDto,
} from './business.dto';

const COLLECTIONS = {
  products: 'products',
  ledger: 'inventoryledgerentries',
  customers: 'customers',
  stores: 'stores',
  purchaseOrders: 'purchaseorders',
  invoices: 'store_invoices',
} as const;

@Injectable()
export class BusinessService {
  constructor(private readonly registry: ConnectionRegistryService) {}

  databases() {
    return this.registry.list();
  }

  health() {
    return this.registry.health();
  }

  inventory(query: InventoryQueryDto) {
    return this.registry.query(query.databaseKey, async (db) => {
      const match = this.inventoryMatch(query);
      const grouped = await db
        .collection(COLLECTIONS.ledger)
        .aggregate([
          { $addFields: { normalizedLocation: { $ifNull: ['$locationKind', 'warehouse'] } } },
          { $match: match },
          {
            $group: {
              _id: {
                sku: '$sku',
                locationKind: '$normalizedLocation',
                storeId: { $ifNull: ['$storeId', null] },
                locationCode: { $ifNull: ['$locationCode', null] },
              },
              quantity: { $sum: '$qtyDelta' },
              lastMovementAt: { $max: '$createdAt' },
            },
          },
          { $match: { quantity: { $ne: 0 } } },
          { $sort: { '_id.sku': 1 } },
        ])
        .toArray();

      const skus = grouped.map((row) => String(row._id.sku));
      const products = skus.length
        ? await db.collection(COLLECTIONS.products).find({ sku: { $in: skus } }).toArray()
        : [];
      const productBySku = new Map(products.map((product) => [String(product.sku), product]));
      const needle = query.search?.trim().toLowerCase();
      const rows = grouped
        .map((row) => {
          const product = productBySku.get(String(row._id.sku));
          return {
            sku: String(row._id.sku),
            locationKind: row._id.locationKind,
            storeId: row._id.storeId,
            locationCode: row._id.locationCode,
            quantity: row.quantity,
            lastMovementAt: row.lastMovementAt,
            product: product ?? null,
          };
        })
        .filter((row) => !needle || this.matchesProduct(row.product, row.sku, needle));
      return this.paginateRows(rows, query.page, query.limit);
    });
  }

  inventorySummary(query: InventoryQueryDto) {
    return this.registry.query(query.databaseKey, async (db) => {
      const [summary] = await db
        .collection(COLLECTIONS.ledger)
        .aggregate([
          { $addFields: { normalizedLocation: { $ifNull: ['$locationKind', 'warehouse'] } } },
          { $match: this.inventoryMatch(query) },
          {
            $group: {
              _id: { sku: '$sku', locationKind: '$normalizedLocation' },
              quantity: { $sum: '$qtyDelta' },
            },
          },
          { $match: { quantity: { $ne: 0 } } },
          {
            $group: {
              _id: null,
              skuCount: { $sum: 1 },
              units: { $sum: '$quantity' },
              warehouseUnits: {
                $sum: { $cond: [{ $eq: ['$_id.locationKind', 'warehouse'] }, '$quantity', 0] },
              },
              storeUnits: {
                $sum: { $cond: [{ $eq: ['$_id.locationKind', 'store'] }, '$quantity', 0] },
              },
              inTransitUnits: {
                $sum: { $cond: [{ $eq: ['$_id.locationKind', 'in_transit'] }, '$quantity', 0] },
              },
            },
          },
        ])
        .toArray();
      return summary ?? { skuCount: 0, units: 0, warehouseUnits: 0, storeUnits: 0, inTransitUnits: 0 };
    });
  }

  inventoryDetail(databaseKey: string | undefined, sku: string) {
    if (!sku.trim()) throw new BadRequestException('sku is required');
    return this.registry.query(databaseKey, async (db) => {
      const product = await db.collection(COLLECTIONS.products).findOne({ sku });
      const balances = await db
        .collection(COLLECTIONS.ledger)
        .aggregate([
          { $match: { sku } },
          { $addFields: { locationKind: { $ifNull: ['$locationKind', 'warehouse'] } } },
          {
            $group: {
              _id: {
                locationKind: '$locationKind',
                storeId: { $ifNull: ['$storeId', null] },
                locationCode: { $ifNull: ['$locationCode', null] },
              },
              quantity: { $sum: '$qtyDelta' },
            },
          },
          { $match: { quantity: { $ne: 0 } } },
        ])
        .toArray();
      return { sku, product, balances };
    });
  }

  async adjustInventory(dto: InventoryAdjustmentDto) {
    const db = await this.writeDb(dto.databaseKey);
    const sku = dto.sku.trim();
    const storeId = dto.storeId?.trim().toLowerCase();
    const locationCode = dto.locationCode?.trim().toLowerCase();
    if (dto.locationKind === 'store' && !storeId) {
      throw new BadRequestException('storeId is required for store adjustments');
    }
    if (dto.locationKind !== 'store' && !locationCode) {
      throw new BadRequestException('locationCode is required for warehouse and in_transit adjustments');
    }
    const identity = {
      sourceType: 'B2BAdjustment',
      sourceId: dto.sourceId.trim(),
      sku,
      locationKind: dto.locationKind,
      ...(storeId ? { storeId } : {}),
      ...(locationCode ? { locationCode } : {}),
    };
    const collection = db.collection(COLLECTIONS.ledger);
    const existing = await collection.findOne(identity);
    if (existing) return { databaseKey: dto.databaseKey, created: false, adjustment: existing };
    const now = new Date();
    const adjustment = { ...identity, qtyDelta: dto.qtyDelta, ...(dto.note ? { note: dto.note } : {}), createdAt: now, updatedAt: now };
    const result = await collection.insertOne(adjustment);
    return { databaseKey: dto.databaseKey, created: true, adjustment: { _id: result.insertedId, ...adjustment } };
  }

  products(query: ScopeQueryDto) {
    return this.listCollection(COLLECTIONS.products, query, ['sku', 'itemName', 'shortName', 'alias', 'upcEanCode']);
  }

  productDetail(databaseKey: string | undefined, idOrSku: string) {
    return this.registry.query(databaseKey, async (db) => {
      const filter = ObjectId.isValid(idOrSku) ? { $or: [{ _id: new ObjectId(idOrSku) }, { sku: idOrSku }] } : { sku: idOrSku };
      const product = await db.collection(COLLECTIONS.products).findOne(filter);
      if (!product) throw new NotFoundException(`Product '${idOrSku}' not found`);
      return product;
    });
  }

  async patchProduct(id: string, dto: ProductPatchDto) {
    const { databaseKey, ...changes } = dto;
    return this.updateById(COLLECTIONS.products, databaseKey, id, changes);
  }

  customers(query: ScopeQueryDto) {
    return this.listCollection(COLLECTIONS.customers, query, ['customerCode', 'name', 'phone', 'email']);
  }

  async createCustomer(dto: CustomerWriteDto) {
    const { databaseKey, ...data } = dto;
    return this.insert(COLLECTIONS.customers, databaseKey, { isActive: true, isCreditCustomer: false, ...data });
  }

  async updateCustomer(id: string, dto: CustomerWriteDto) {
    const { databaseKey, ...data } = dto;
    return this.updateById(COLLECTIONS.customers, databaseKey, id, data);
  }

  deactivateCustomer(id: string, databaseKey: string | undefined) {
    return this.updateById(COLLECTIONS.customers, requireWriteDatabaseKey(databaseKey), id, { isActive: false });
  }

  stores(query: ScopeQueryDto) {
    return this.listCollection(COLLECTIONS.stores, query, ['code', 'name', 'address', 'phone']);
  }

  async createStore(dto: StoreWriteDto) {
    const { databaseKey, ...data } = dto;
    return this.insert(COLLECTIONS.stores, databaseKey, {
      status: 'active',
      preferCentralOnline: false,
      ...data,
      code: data.code.trim().toLowerCase(),
    });
  }

  async updateStore(id: string, dto: StoreWriteDto) {
    const { databaseKey, ...data } = dto;
    return this.updateById(COLLECTIONS.stores, databaseKey, id, { ...data, code: data.code.trim().toLowerCase() });
  }

  deactivateStore(id: string, databaseKey: string | undefined) {
    return this.updateById(COLLECTIONS.stores, requireWriteDatabaseKey(databaseKey), id, { status: 'inactive' });
  }

  purchaseOrders(query: ScopeQueryDto) {
    return this.listCollection(COLLECTIONS.purchaseOrders, query, ['poNo', 'supplier.name', 'supplier.code', 'status']);
  }

  async createPurchaseOrder(dto: PurchaseOrderWriteDto) {
    const { databaseKey, ...data } = dto;
    return this.insert(COLLECTIONS.purchaseOrders, databaseKey, { status: 'open', ...data });
  }

  async updatePurchaseOrder(id: string, dto: PurchaseOrderWriteDto) {
    const { databaseKey, ...data } = dto;
    return this.updateById(COLLECTIONS.purchaseOrders, databaseKey, id, data);
  }

  async setPurchaseOrderStatus(id: string, dto: PurchaseOrderStatusDto) {
    return this.updateById(COLLECTIONS.purchaseOrders, dto.databaseKey, id, { status: dto.status });
  }

  bills(query: ScopeQueryDto) {
    return this.listCollection(COLLECTIONS.invoices, query, ['invoiceNo', 'storeId', 'payload.customerName', 'payload.phone']);
  }

  record(
    collection: 'customers' | 'stores' | 'purchaseorders' | 'store_invoices',
    databaseKey: string | undefined,
    id: string,
  ) {
    const objectId = this.objectId(id);
    return this.registry.query(databaseKey, async (db) => {
      const data = await db.collection(collection).findOne({ _id: objectId });
      if (!data) throw new NotFoundException(`Record '${id}' not found`);
      return data;
    });
  }

  reports(query: DateRangeQueryDto) {
    const range = this.dateRange(query.from, query.to);
    return this.registry.query(query.databaseKey, async (db) => {
      const ledgerPipeline: Document[] = [
        { $group: { _id: '$sku', quantity: { $sum: '$qtyDelta' } } },
        { $match: { quantity: { $gt: 0 } } },
        { $lookup: { from: COLLECTIONS.products, localField: '_id', foreignField: 'sku', as: 'product' } },
        { $unwind: { path: '$product', preserveNullAndEmptyArrays: true } },
        {
          $group: {
            _id: null,
            inventorySkus: { $sum: 1 },
            inventoryUnits: { $sum: '$quantity' },
            costValue: { $sum: { $multiply: ['$quantity', { $ifNull: ['$product.costPrice', 0] }] } },
            sellingValue: { $sum: { $multiply: ['$quantity', { $ifNull: ['$product.sellingPrice', 0] }] } },
          },
        },
      ];
      const invoiceMatch = range ? { createdAt: range } : {};
      const [inventoryRows, customerCount, storeCount, orderRows, salesRows] = await Promise.all([
        db.collection(COLLECTIONS.ledger).aggregate(ledgerPipeline).toArray(),
        db.collection(COLLECTIONS.customers).countDocuments(),
        db.collection(COLLECTIONS.stores).countDocuments(),
        db.collection(COLLECTIONS.purchaseOrders).aggregate([
          ...(range ? [{ $match: { createdAt: range } }] : []),
          { $group: { _id: null, count: { $sum: 1 }, total: { $sum: { $ifNull: ['$netAmount', 0] } } } },
        ]).toArray(),
        db.collection(COLLECTIONS.invoices).aggregate([
          { $match: invoiceMatch },
          {
            $group: {
              _id: null,
              count: { $sum: 1 },
              total: {
                $sum: {
                  $ifNull: ['$payload.netAmount', { $ifNull: ['$payload.grandTotal', { $ifNull: ['$payload.total', 0] }] }],
                },
              },
            },
          },
        ]).toArray(),
      ]);
      return {
        inventory: inventoryRows[0] ?? { inventorySkus: 0, inventoryUnits: 0, costValue: 0, sellingValue: 0 },
        customers: customerCount,
        stores: storeCount,
        purchaseOrders: orderRows[0] ?? { count: 0, total: 0 },
        sales: salesRows[0] ?? { count: 0, total: 0 },
      };
    });
  }

  private listCollection(collection: string, query: ScopeQueryDto, fields: string[]) {
    return this.registry.query(query.databaseKey, async (db) => {
      const filter = this.searchFilter(query.search, fields);
      const skip = (query.page - 1) * query.limit;
      const [data, total] = await Promise.all([
        db.collection(collection).find(filter).sort({ updatedAt: -1, _id: -1 }).skip(skip).limit(query.limit).toArray(),
        db.collection(collection).countDocuments(filter),
      ]);
      return { data, total, page: query.page, limit: query.limit, totalPages: Math.ceil(total / query.limit) };
    });
  }

  private async insert(collection: string, databaseKey: string, data: Record<string, unknown>) {
    const db = await this.writeDb(databaseKey);
    const now = new Date();
    const document = { ...data, createdAt: now, updatedAt: now };
    try {
      const result = await db.collection(collection).insertOne(document);
      return { databaseKey, data: { _id: result.insertedId, ...document } };
    } catch (error) {
      this.handleMongoError(error);
    }
  }

  private async updateById(collection: string, databaseKey: string, id: string, changes: Record<string, unknown>) {
    const db = await this.writeDb(databaseKey);
    const objectId = this.objectId(id);
    delete changes._id;
    delete changes.createdAt;
    try {
      const data = await db.collection(collection).findOneAndUpdate(
        { _id: objectId },
        { $set: { ...changes, updatedAt: new Date() } },
        { returnDocument: 'after' },
      );
      if (!data) throw new NotFoundException(`Record '${id}' not found`);
      return { databaseKey, data };
    } catch (error) {
      this.handleMongoError(error);
    }
  }

  private async writeDb(databaseKey: string | undefined): Promise<Db> {
    return this.registry.database(requireWriteDatabaseKey(databaseKey));
  }

  private inventoryMatch(query: InventoryQueryDto): Record<string, unknown> {
    const match: Record<string, unknown> = {};
    if (query.locationKind) match.normalizedLocation = query.locationKind;
    if (query.storeId) match.storeId = query.storeId.trim().toLowerCase();
    return match;
  }

  private searchFilter(search: string | undefined, fields: string[]): Record<string, unknown> {
    const value = search?.trim();
    if (!value) return {};
    const escaped = value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    return { $or: fields.map((field) => ({ [field]: { $regex: escaped, $options: 'i' } })) };
  }

  private matchesProduct(product: Document | null | undefined, sku: string, needle: string) {
    if (sku.toLowerCase().includes(needle)) return true;
    if (!product) return false;
    return ['itemName', 'shortName', 'alias', 'upcEanCode'].some(
      (field) => typeof product[field] === 'string' && product[field].toLowerCase().includes(needle),
    );
  }

  private paginateRows<T>(rows: T[], page: number, limit: number) {
    const total = rows.length;
    return {
      data: rows.slice((page - 1) * limit, page * limit),
      total,
      page,
      limit,
      totalPages: Math.ceil(total / limit),
    };
  }

  private objectId(id: string): ObjectId {
    if (!ObjectId.isValid(id)) throw new BadRequestException(`Invalid id '${id}'`);
    return new ObjectId(id);
  }

  private dateRange(from?: string, to?: string): { $gte?: Date; $lte?: Date } | undefined {
    if (!from && !to) return undefined;
    const range: { $gte?: Date; $lte?: Date } = {};
    if (from) {
      const date = new Date(from);
      if (Number.isNaN(date.getTime())) throw new BadRequestException('Invalid from date');
      range.$gte = date;
    }
    if (to) {
      const date = new Date(to);
      if (Number.isNaN(date.getTime())) throw new BadRequestException('Invalid to date');
      range.$lte = date;
    }
    if (range.$gte && range.$lte && range.$gte > range.$lte) {
      throw new BadRequestException('from cannot be after to');
    }
    return range;
  }

  private handleMongoError(error: unknown): never {
    if (error instanceof MongoServerError && error.code === 11000) {
      throw new ConflictException('A record with the same unique value already exists');
    }
    throw error;
  }
}
