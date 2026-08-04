import { BadRequestException, Injectable, NotFoundException } from '@nestjs/common';
import { randomUUID } from 'crypto';
import { Db, Document, ObjectId } from 'mongodb';
import { DatabaseSource } from '../config/database-config';
import { ConnectionRegistryService } from '../database/connection-registry.service';
import { StorefrontCatalogQueryDto, StorefrontEnquiryDto, StorefrontEnquiryLineDto } from './storefront.dto';

const PRODUCTS = 'products';
const LEDGER = 'inventoryledgerentries';
const CATEGORIES = 'categories';
const SUB_CATEGORIES = 'sub_categories';
const CUSTOMERS = 'customers';
const ENQUIRIES = 'b2b_enquiries';

export type StorefrontOffer = {
  id: string;
  databaseKey: string;
  databaseLabel: string;
  productId: string;
  sku: string;
  slug: string;
  name: string;
  price: number;
  mrp: number | null;
  moq: number;
  category: { key: string; name: string };
  subCategory: { key: string; name: string } | null;
  images: string[];
  stock: {
    total: number;
    warehouse: number;
    store: number;
    inTransit: number;
    available: boolean;
  };
  specifications: Record<string, string>;
};

@Injectable()
export class StorefrontService {
  constructor(private readonly registry: ConnectionRegistryService) {}

  async catalog(query: StorefrontCatalogQueryDto) {
    const response = await this.registry.query('all', (db, source) =>
      this.loadOffers(db, source, query.search),
    );
    let data = response.results.flatMap((result) => result.data);
    if (query.category) data = data.filter((offer) => offer.category.key === query.category);
    if (query.minPrice !== undefined) data = data.filter((offer) => offer.price >= query.minPrice!);
    if (query.maxPrice !== undefined) data = data.filter((offer) => offer.price <= query.maxPrice!);
    data.sort(this.offerSorter(query.sort));

    const total = data.length;
    const start = (query.page - 1) * query.limit;
    return {
      data: data.slice(start, start + query.limit),
      total,
      page: query.page,
      limit: query.limit,
      totalPages: Math.ceil(total / query.limit),
      errors: response.errors,
    };
  }

  async categories() {
    const response = await this.registry.query('all', (db, source) => this.loadOffers(db, source));
    const byKey = new Map<string, { key: string; name: string; productCount: number }>();
    for (const offer of response.results.flatMap((result) => result.data)) {
      const current = byKey.get(offer.category.key);
      if (current) current.productCount += 1;
      else byKey.set(offer.category.key, { ...offer.category, productCount: 1 });
    }
    return {
      data: [...byKey.values()].sort((a, b) => a.name.localeCompare(b.name)),
      errors: response.errors,
    };
  }

  async detail(databaseKey: string, productIdOrSku: string): Promise<StorefrontOffer> {
    const source = this.registry.source(databaseKey);
    const db = await this.registry.database(source.key);
    const identity = ObjectId.isValid(productIdOrSku)
      ? { _id: new ObjectId(productIdOrSku) }
      : { sku: productIdOrSku };
    const product = await db.collection(PRODUCTS).findOne({
      ...identity,
      isActive: true,
      isAddedInB2B: true,
    });
    if (!product) throw new NotFoundException('Storefront product offer not found');
    const [offer] = await this.mapOffers(db, source, [product]);
    if (!offer) throw new NotFoundException('Storefront product offer not found');
    return offer;
  }

  async submitEnquiry(dto: StorefrontEnquiryDto) {
    const requestId = dto.requestId?.trim() || randomUUID();
    const groups = new Map<string, StorefrontEnquiryLineDto[]>();
    for (const line of dto.lines) {
      const key = line.databaseKey.trim().toLowerCase();
      groups.set(key, [...(groups.get(key) ?? []), line]);
    }

    const results: Array<{ databaseKey: string; databaseLabel: string; enquiryId: string; created: boolean }> = [];
    const errors: Array<{ databaseKey: string; databaseLabel: string; error: string }> = [];
    await Promise.all(
      [...groups.entries()].map(async ([databaseKey, lines]) => {
        let label = databaseKey;
        try {
          const source = this.registry.source(databaseKey);
          label = source.label;
          const db = await this.registry.database(databaseKey);
          const result = await this.writeEnquiry(db, source, requestId, dto, lines);
          results.push(result);
        } catch (error) {
          errors.push({
            databaseKey,
            databaseLabel: label,
            error: error instanceof BadRequestException || error instanceof NotFoundException
              ? error.message
              : 'Unable to submit enquiry to this business',
          });
        }
      }),
    );
    const order = new Map([...groups.keys()].map((key, index) => [key, index]));
    results.sort((a, b) => order.get(a.databaseKey)! - order.get(b.databaseKey)!);
    errors.sort((a, b) => order.get(a.databaseKey)! - order.get(b.databaseKey)!);
    return { requestId, results, errors };
  }

  private async loadOffers(db: Db, source: DatabaseSource, search?: string): Promise<StorefrontOffer[]> {
    const filter: Document = { isActive: true, isAddedInB2B: true };
    const needle = search?.trim();
    if (needle) {
      const escaped = needle.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
      filter.$or = ['sku', 'itemName', 'shortName', 'alias', 'upcEanCode'].map((field) => ({
        [field]: { $regex: escaped, $options: 'i' },
      }));
    }
    const products = await db.collection(PRODUCTS).find(filter).limit(2_000).toArray();
    return this.mapOffers(db, source, products);
  }

  private async mapOffers(db: Db, source: DatabaseSource, products: Document[]): Promise<StorefrontOffer[]> {
    if (products.length === 0) return [];
    const skus = products.map((product) => String(product.sku ?? '')).filter(Boolean);
    const categoryIds = [...new Set(products.map((product) => this.refString(product.categoryId)).filter(Boolean))];
    const subCategoryIds = [...new Set(products.map((product) => this.refString(product.subCategoryId)).filter(Boolean))];
    const [balances, categories, subCategories] = await Promise.all([
      this.stockBySku(db, skus),
      this.findByIds(db, CATEGORIES, categoryIds),
      this.findByIds(db, SUB_CATEGORIES, subCategoryIds),
    ]);
    const categoryMap = new Map(categories.map((item) => [String(item._id), item]));
    const subCategoryMap = new Map(subCategories.map((item) => [String(item._id), item]));

    return products.map((product) => {
      const productId = String(product._id);
      const sku = String(product.sku ?? productId);
      const categoryDoc = categoryMap.get(this.refString(product.categoryId));
      const subCategoryDoc = subCategoryMap.get(this.refString(product.subCategoryId));
      const categoryName = String(categoryDoc?.name ?? product.itemProductType ?? 'Collections');
      const categoryKey = this.slugify(categoryName) || 'collections';
      const subCategoryName = subCategoryDoc?.name ? String(subCategoryDoc.name) : '';
      const balance = balances.get(sku) ?? { warehouse: 0, store: 0, inTransit: 0 };
      const total = balance.warehouse + balance.store;
      const price = this.number(product.storePrice ?? product.sellingPrice ?? product.mrp ?? 0);
      const mrp = product.mrp == null ? null : this.number(product.mrp);
      const name = String(product.itemName ?? product.shortName ?? sku);
      return {
        id: `${source.key}:${productId}`,
        databaseKey: source.key,
        databaseLabel: source.label,
        productId,
        sku,
        slug: `${source.key}-${this.slugify(name)}-${productId.slice(-6)}`,
        name,
        price,
        mrp,
        moq: Math.max(1, Math.round(this.number(product.itemPerUnit ?? product.minimumShelfFit ?? 1))),
        category: { key: categoryKey, name: categoryName },
        subCategory: subCategoryName
          ? { key: this.slugify(subCategoryName), name: subCategoryName }
          : null,
        images: this.resolveImages(source, product),
        stock: {
          total,
          warehouse: balance.warehouse,
          store: balance.store,
          inTransit: balance.inTransit,
          available: total > 0,
        },
        specifications: {
          SKU: sku,
          ...(product.unit ? { Unit: String(product.unit) } : {}),
          ...(product.itemProductType ? { Type: String(product.itemProductType) } : {}),
          ...(product.upcEanCode ? { Barcode: String(product.upcEanCode) } : {}),
        },
      };
    });
  }

  private async stockBySku(db: Db, skus: string[]) {
    const rows = await db.collection(LEDGER).aggregate<{
      _id: { sku: string; locationKind: string };
      quantity: number;
    }>([
      { $match: { sku: { $in: skus } } },
      { $addFields: { normalizedLocation: { $ifNull: ['$locationKind', 'warehouse'] } } },
      { $group: { _id: { sku: '$sku', locationKind: '$normalizedLocation' }, quantity: { $sum: '$qtyDelta' } } },
    ]).toArray();
    const result = new Map<string, { warehouse: number; store: number; inTransit: number }>();
    for (const row of rows) {
      const current = result.get(row._id.sku) ?? { warehouse: 0, store: 0, inTransit: 0 };
      if (row._id.locationKind === 'warehouse') current.warehouse += row.quantity;
      else if (row._id.locationKind === 'store') current.store += row.quantity;
      else if (row._id.locationKind === 'in_transit') current.inTransit += row.quantity;
      result.set(row._id.sku, current);
    }
    return result;
  }

  private async writeEnquiry(
    db: Db,
    source: DatabaseSource,
    requestId: string,
    dto: StorefrontEnquiryDto,
    lines: StorefrontEnquiryLineDto[],
  ) {
    const collection = db.collection(ENQUIRIES);
    await collection.createIndex({ requestId: 1 }, { unique: true });
    const existing = await collection.findOne({ requestId });
    if (existing) {
      return {
        databaseKey: source.key,
        databaseLabel: source.label,
        enquiryId: String(existing._id),
        created: false,
      };
    }

    const ids = lines.map((line) => new ObjectId(line.productId));
    const products = await db.collection(PRODUCTS).find({
      _id: { $in: ids },
      isActive: true,
      isAddedInB2B: true,
    }).toArray();
    const byId = new Map(products.map((product) => [String(product._id), product]));
    if (byId.size !== new Set(lines.map((line) => line.productId)).size) {
      throw new BadRequestException('One or more products are unavailable for B2B');
    }
    const balances = await this.stockBySku(db, products.map((product) => String(product.sku)));
    const enquiryLines = lines.map((line) => {
      const product = byId.get(line.productId)!;
      const sku = String(product.sku);
      const balance = balances.get(sku);
      const available = (balance?.warehouse ?? 0) + (balance?.store ?? 0);
      if (line.quantity > available) {
        throw new BadRequestException(`Insufficient stock for ${sku}; ${available} available`);
      }
      const unitPrice = this.number(product.storePrice ?? product.sellingPrice ?? product.mrp ?? 0);
      return {
        productId: product._id,
        sku,
        name: String(product.itemName ?? sku),
        quantity: line.quantity,
        unitPrice,
        amount: unitPrice * line.quantity,
      };
    });
    const now = new Date();
    const retailer = {
      name: dto.retailer.businessName.trim(),
      email: dto.retailer.email.trim().toLowerCase(),
      phone: dto.retailer.phone.trim(),
      addressLine1: dto.retailer.address?.trim(),
      city: dto.retailer.city?.trim(),
      state: dto.retailer.state?.trim(),
      pincode: dto.retailer.pincode?.trim(),
      isActive: true,
      isCreditCustomer: false,
      updatedAt: now,
    };
    const customer = await db.collection(CUSTOMERS).findOneAndUpdate(
      { $or: [{ email: retailer.email }, { phone: retailer.phone }] },
      { $set: retailer, $setOnInsert: { createdAt: now } },
      { upsert: true, returnDocument: 'after' },
    );
    const document = {
      requestId,
      status: 'new',
      source: 'b2b-storefront',
      retailer: { ...dto.retailer, customerId: customer?._id ?? null },
      lines: enquiryLines,
      totalAmount: enquiryLines.reduce((sum, line) => sum + line.amount, 0),
      ...(dto.note ? { note: dto.note.trim() } : {}),
      createdAt: now,
      updatedAt: now,
    };
    const inserted = await collection.insertOne(document);
    return {
      databaseKey: source.key,
      databaseLabel: source.label,
      enquiryId: String(inserted.insertedId),
      created: true,
    };
  }

  private async findByIds(db: Db, collection: string, ids: string[]) {
    const objectIds = ids.filter(ObjectId.isValid).map((id) => new ObjectId(id));
    return objectIds.length
      ? db.collection(collection).find({ _id: { $in: objectIds } }).toArray()
      : [];
  }

  private refString(value: unknown): string {
    if (!value) return '';
    if (value instanceof ObjectId) return value.toHexString();
    if (typeof value === 'object' && '_id' in value!) return String((value as { _id: unknown })._id);
    return String(value);
  }

  private resolveImages(source: DatabaseSource, product: Document): string[] {
    const raw = Array.isArray(product.mediaItems)
      ? product.mediaItems.map((item) => item?.url).filter((url): url is string => typeof url === 'string')
      : Array.isArray(product.mediaUrls)
        ? product.mediaUrls.filter((url): url is string => typeof url === 'string')
        : [];
    return [...new Set(raw.map((url) => this.resolveMediaUrl(source.publicApiBaseUrl, url)))];
  }

  private resolveMediaUrl(base: string | undefined, value: string): string {
    if (/^https?:\/\//i.test(value) || !base) return value;
    try {
      return new URL(value, `${base}/`).toString();
    } catch {
      return value;
    }
  }

  private slugify(value: string): string {
    return value.toLowerCase().trim().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
  }

  private number(value: unknown): number {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
  }

  private offerSorter(sort: StorefrontCatalogQueryDto['sort']) {
    if (sort === 'price-asc') return (a: StorefrontOffer, b: StorefrontOffer) => a.price - b.price;
    if (sort === 'price-desc') return (a: StorefrontOffer, b: StorefrontOffer) => b.price - a.price;
    if (sort === 'name') return (a: StorefrontOffer, b: StorefrontOffer) => a.name.localeCompare(b.name);
    return (a: StorefrontOffer, b: StorefrontOffer) =>
      Number(b.stock.available) - Number(a.stock.available) || a.name.localeCompare(b.name);
  }
}
