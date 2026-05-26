import { BadRequestException, ConflictException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model, SortOrder } from 'mongoose';
import {
  PRODUCT_REF_OBJECT_ID_FIELDS,
  isValidObjectIdString,
  stripInvalidObjectIdRefs,
  toObjectId,
} from '../../common/object-id.util';
import { InventoryService } from '../inventory/inventory.service';
import { Product, ProductDocument } from '../products/schemas/product.schema';
import { CreateGoodsReceiptDto, CreateGoodsReceiptLineDto } from './dto/create-goods-receipt.dto';
import { FilterGoodsReceiptDto } from './dto/filter-goods-receipt.dto';
import { UpdateGoodsReceiptDto } from './dto/update-goods-receipt.dto';
import { GoodsReceiptNumberGenerator } from './goods-receipt-number.generator';
import { GoodsReceipt, GoodsReceiptDocument } from './schemas/goods-receipt.schema';

@Injectable()
export class GoodsReceiptsService {
  constructor(
    @InjectModel(GoodsReceipt.name) private readonly grModel: Model<GoodsReceiptDocument>,
    @InjectModel(Product.name) private readonly productModel: Model<ProductDocument>,
    private readonly inventoryService: InventoryService,
    private readonly receiptNumbers: GoodsReceiptNumberGenerator,
  ) {}

  private async resolveReceiptNo(input?: string): Promise<string> {
    const trimmed = input?.trim();
    if (trimmed) {
      const exists = await this.grModel.exists({ receiptNo: trimmed }).lean();
      if (exists) throw new ConflictException(`Receipt number '${trimmed}' is already in use`);
      return trimmed;
    }
    return await this.receiptNumbers.allocateReceiptNoAsync();
  }

  private async resolveGrnNumber(input?: string): Promise<string> {
    const trimmed = input?.trim();
    if (trimmed) {
      const exists = await this.grModel.exists({ grnNumber: trimmed }).lean();
      if (exists) throw new ConflictException(`GRN number '${trimmed}' is already in use`);
      return trimmed;
    }
    return await this.receiptNumbers.allocateGrnNumberAsync();
  }

  private async normalizeLines(lines: CreateGoodsReceiptLineDto[]) {
    const normalized: Array<Record<string, unknown>> = [];
    for (const line of lines) {
      const sku = line.sku.trim();
      if (!sku) throw new BadRequestException('Each line requires a non-empty sku');

      let productId = line.productId?.trim();
      if (productId && !isValidObjectIdString(productId)) {
        throw new BadRequestException(`Invalid productId '${productId}'`);
      }

      if (!productId) {
        const bySku = await this.productModel.findOne({ sku }).select('_id sku').lean();
        if (bySku?._id) productId = String(bySku._id);
      } else {
        const exists = await this.productModel.findById(toObjectId(productId)).select('_id sku').lean();
        if (!exists) throw new NotFoundException(`Product not found for id '${productId}'`);
        if (exists.sku !== sku) {
          throw new BadRequestException(
            `productId '${productId}' does not match line sku '${sku}' (product sku is '${exists.sku}')`,
          );
        }
      }

      normalized.push({
        ...(productId ? { productId: toObjectId(productId) } : {}),
        sku,
        description: line.description,
        orderedQty: line.orderedQty,
        receivedQty: line.receivedQty,
        outcome: line.outcome,
      });
    }
    return normalized;
  }

  /** Attaches populated `product` on each line (API response only). */
  private async attachLineProducts<T extends { lines?: Array<Record<string, unknown>> }>(docs: T[]): Promise<T[]> {
    const productIds = new Set<string>();
    const skus = new Set<string>();
    for (const doc of docs) {
      for (const line of doc.lines ?? []) {
        const pid = line.productId;
        if (pid != null && String(pid).trim() !== '') {
          productIds.add(String(pid));
        } else if (typeof line.sku === 'string' && line.sku.trim()) {
          skus.add(line.sku.trim());
        }
      }
    }

    const byId = new Map<string, Record<string, unknown>>();
    if (productIds.size > 0) {
      const ids = [...productIds].filter((id) => isValidObjectIdString(id)).map((id) => toObjectId(id));
      const rows = await this.productModel.find({ _id: { $in: ids } }).lean();
      const cleaned = rows.map((row) =>
        stripInvalidObjectIdRefs({ ...row } as Record<string, unknown>, PRODUCT_REF_OBJECT_ID_FIELDS),
      );
      await this.productModel.populate(
        cleaned,
        PRODUCT_REF_OBJECT_ID_FIELDS.map((path) => ({ path })),
      );
      for (const row of cleaned) {
        if (row._id != null) byId.set(String(row._id), row);
      }
    }

    const bySku = new Map<string, Record<string, unknown>>();
    if (skus.size > 0) {
      const rows = await this.productModel.find({ sku: { $in: [...skus] } }).lean();
      const cleaned = rows.map((row) =>
        stripInvalidObjectIdRefs({ ...row } as Record<string, unknown>, PRODUCT_REF_OBJECT_ID_FIELDS),
      );
      await this.productModel.populate(
        cleaned,
        PRODUCT_REF_OBJECT_ID_FIELDS.map((path) => ({ path })),
      );
      for (const row of cleaned) {
        if (typeof row.sku === 'string') bySku.set(row.sku, row);
      }
    }

    return docs.map((doc) => ({
      ...doc,
      lines: (doc.lines ?? []).map((line) => {
        const pid = line.productId != null ? String(line.productId) : '';
        const sku = typeof line.sku === 'string' ? line.sku.trim() : '';
        const product =
          (pid && byId.get(pid)) ??
          (sku && bySku.get(sku)) ??
          null;
        return { ...line, product };
      }),
    }));
  }

  private async enrichOne(doc: Record<string, unknown>) {
    const [enriched] = await this.attachLineProducts([doc as { lines?: Array<Record<string, unknown>> }]);
    return enriched;
  }

  async create(dto: CreateGoodsReceiptDto) {
    const receiptNo = await this.resolveReceiptNo();
    const grnNumber = await this.resolveGrnNumber(dto.grnNumber);
    const lines = dto.lines?.length ? await this.normalizeLines(dto.lines) : [];
    const created = await this.grModel.create({
      receiptNo,
      poId: dto.poId,
      poNo: dto.poNo,
      grnNumber,
      supplier: dto.supplier,
      invoiceNo: dto.invoiceNo,
      invoiceDate: dto.invoiceDate,
      remarks: dto.remarks,
      status: (dto.status as GoodsReceipt['status']) ?? 'draft',
      lines,
    });
    return await this.enrichOne(created.toObject() as unknown as Record<string, unknown>);
  }

  async findById(id: string) {
    const doc = await this.grModel.findById(id).lean();
    if (!doc) throw new NotFoundException('Goods receipt not found');
    return await this.enrichOne(doc as Record<string, unknown>);
  }

  async update(id: string, dto: UpdateGoodsReceiptDto) {
    const set: Record<string, unknown> = { ...dto };
    if (dto.lines !== undefined) {
      set.lines = dto.lines.length ? await this.normalizeLines(dto.lines) : [];
    }
    const doc = await this.grModel.findByIdAndUpdate(id, { $set: set }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Goods receipt not found');
    return await this.enrichOne(doc as Record<string, unknown>);
  }

  async list(params: { search?: string; poNo?: string; grnNumber?: string; status?: string }) {
    const filter: Record<string, unknown> = {};
    if (params.poNo) filter.poNo = params.poNo;
    if (params.grnNumber) filter.grnNumber = params.grnNumber;
    if (params.status) filter.status = params.status;
    if (params.search) {
      filter.$or = [
        { receiptNo: { $regex: params.search, $options: 'i' } },
        { grnNumber: { $regex: params.search, $options: 'i' } },
        { poNo: { $regex: params.search, $options: 'i' } },
        { invoiceNo: { $regex: params.search, $options: 'i' } },
        { 'supplier.name': { $regex: params.search, $options: 'i' } },
      ];
    }
    const rows = await this.grModel.find(filter).sort({ updatedAt: -1 }).limit(200).lean();
    return await this.attachLineProducts(rows as Array<{ lines?: Array<Record<string, unknown>> }>);
  }

  async filter(dto: FilterGoodsReceiptDto) {
    const filter: FilterQuery<GoodsReceiptDocument> = {};

    if (dto.receiptNo) filter.receiptNo = dto.receiptNo;
    if (dto.poId) filter.poId = dto.poId;
    if (dto.poNo) filter.poNo = dto.poNo;
    if (dto.grnNumber) filter.grnNumber = dto.grnNumber;
    if (dto.invoiceNo) filter.invoiceNo = dto.invoiceNo;
    if (dto.supplierId) filter['supplier.supplierId'] = dto.supplierId;
    if (dto.status) filter.status = dto.status;

    if (dto.invoiceDateFrom || dto.invoiceDateTo) {
      filter.invoiceDate = {};
      if (dto.invoiceDateFrom) filter.invoiceDate.$gte = dto.invoiceDateFrom;
      if (dto.invoiceDateTo) filter.invoiceDate.$lte = dto.invoiceDateTo;
    }

    if (dto.search) {
      filter.$or = [
        { receiptNo: { $regex: dto.search, $options: 'i' } },
        { grnNumber: { $regex: dto.search, $options: 'i' } },
        { poNo: { $regex: dto.search, $options: 'i' } },
        { invoiceNo: { $regex: dto.search, $options: 'i' } },
        { 'supplier.name': { $regex: dto.search, $options: 'i' } },
      ];
    }

    const page = dto.page ?? 1;
    const limit = dto.limit ?? 20;
    const skip = (page - 1) * limit;
    const sortBy = dto.sortBy ?? 'updatedAt';
    const sortOrder: SortOrder = dto.sortOrder === 'asc' ? 1 : -1;

    const [data, total] = await Promise.all([
      this.grModel
        .find(filter)
        .sort({ [sortBy]: sortOrder })
        .skip(skip)
        .limit(limit)
        .lean(),
      this.grModel.countDocuments(filter),
    ]);

    const enrichedData = await this.attachLineProducts(
      data as Array<{ lines?: Array<Record<string, unknown>> }>,
    );

    return {
      data: enrichedData,
      total,
      page,
      limit,
      totalPages: Math.ceil(total / limit),
    };
  }

  async postToInventory(id: string) {
    const doc = await this.grModel.findById(id);
    if (!doc) throw new NotFoundException('Goods receipt not found');

    const lines = doc.lines ?? [];
    const ledgerEntries = lines
      .filter((l) => (l.outcome ?? 'valid') === 'valid')
      .map((l) => ({
        sku: l.sku,
        qtyDelta: l.receivedQty ?? 0,
        sourceType: 'GoodsReceiptPosted',
        sourceId: String(doc._id),
        note: doc.receiptNo,
        locationKind: 'warehouse' as const,
      }))
      .filter((e) => e.qtyDelta !== 0);

    await this.inventoryService.addLedgerEntries(ledgerEntries);
    doc.status = 'posted';
    await doc.save();
    return await this.enrichOne(doc.toObject() as unknown as Record<string, unknown>);
  }
}
