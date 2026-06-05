import { BadRequestException, ConflictException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model, SortOrder } from 'mongoose';
import { DocumentNumberService } from '../../common/document-number.service';
import { isValidObjectIdString, toObjectId } from '../../common/object-id.util';
import {
  attachLineProducts,
  enrichDocWithLineProducts,
  resolveProductIdForSku,
} from '../../common/product-line-enrichment';
import { DocumentNumberAllocatorService } from '../document-numbers/document-number-allocator.service';
import { DocumentNumberConfigService } from '../document-numbers/document-number-config.service';
import { GoodsReceipt, GoodsReceiptDocument } from '../goods-receipts/schemas/goods-receipt.schema';
import { Product, ProductDocument } from '../products/schemas/product.schema';
import { CreatePurchaseOrderDto, CreatePurchaseOrderLineDto } from './dto/create-purchase-order.dto';
import { FilterPurchaseOrderDto } from './dto/filter-purchase-order.dto';
import { UpdatePurchaseOrderDto } from './dto/update-purchase-order.dto';
import {
  PoRefreshProductSnapshot,
  refreshPurchaseOrderLine,
  rollupPurchaseOrderHeaderTotals,
} from './purchase-order-line-calculator';
import {
  PurchaseOrder,
  PurchaseOrderDocument,
  PurchaseOrderLine,
  PurchaseOrderStatus,
} from './schemas/purchase-order.schema';

const REFRESHABLE_PO_STATUSES: PurchaseOrderStatus[] = ['open', 'awaiting_approval'];
const DELETABLE_PO_STATUSES: PurchaseOrderStatus[] = ['open', 'awaiting_approval'];

@Injectable()
export class PurchaseOrdersService {
  constructor(
    @InjectModel(PurchaseOrder.name) private readonly poModel: Model<PurchaseOrderDocument>,
    @InjectModel(Product.name) private readonly productModel: Model<ProductDocument>,
    @InjectModel(GoodsReceipt.name) private readonly grModel: Model<GoodsReceiptDocument>,
    private readonly allocator: DocumentNumberAllocatorService,
    private readonly configService: DocumentNumberConfigService,
  ) {}

  private async allocatePoNo(): Promise<string> {
    const config = await this.configService.getByKey('purchase_order');
    const prefix = config.prefix;
    return this.allocator.allocate('purchase_order', {
      exists: async (v) => !!(await this.poModel.exists({ poNo: v }).lean()),
      syncFloorFromValues: () => this.maxPoSequenceForPrefix(prefix),
    });
  }

  private async maxPoSequenceForPrefix(prefix: string): Promise<number> {
    const escaped = prefix.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const regex = new RegExp(`^${escaped}\\d+$`, 'i');
    const rows = await this.poModel.find({ poNo: regex }).select('poNo').lean();
    let max = 0;
    for (const row of rows) {
      if (typeof row.poNo !== 'string') continue;
      const n = DocumentNumberService.parseSequenceNumber(row.poNo, prefix);
      if (n !== null && n > max) max = n;
    }
    return max;
  }

  private async normalizeDtoLine(dto: CreatePurchaseOrderLineDto): Promise<PurchaseOrderLine> {
    const sku = dto.sku.trim();
    const productId = await resolveProductIdForSku(this.productModel, sku, dto.productId);
    const line: PurchaseOrderLine = { sku };
    if (productId) line.productId = productId;
    if (dto.barcode !== undefined) line.barcode = dto.barcode;
    if (dto.description !== undefined) line.description = dto.description;
    if (dto.recdQty !== undefined) line.recdQty = dto.recdQty;
    if (dto.freeQty !== undefined) line.freeQty = dto.freeQty;
    if (dto.cost !== undefined) line.cost = dto.cost;
    if (dto.selling !== undefined) line.selling = dto.selling;
    if (dto.mrp !== undefined) line.mrp = dto.mrp;
    if (dto.discountPercent !== undefined) line.discountPercent = dto.discountPercent;
    if (dto.discountAmount !== undefined) line.discountAmount = dto.discountAmount;
    if (dto.taxPercent !== undefined) line.taxPercent = dto.taxPercent;
    if (dto.taxAmount !== undefined) line.taxAmount = dto.taxAmount;
    if (dto.cgstPercent !== undefined) line.cgstPercent = dto.cgstPercent;
    if (dto.cgstAmount !== undefined) line.cgstAmount = dto.cgstAmount;
    if (dto.sgstPercent !== undefined) line.sgstPercent = dto.sgstPercent;
    if (dto.sgstAmount !== undefined) line.sgstAmount = dto.sgstAmount;
    if (dto.surchargePercent !== undefined) line.surchargePercent = dto.surchargePercent;
    if (dto.surchargeAmount !== undefined) line.surchargeAmount = dto.surchargeAmount;
    if (dto.amount !== undefined) line.amount = dto.amount;
    if (dto.netCost !== undefined) line.netCost = dto.netCost;
    if (dto.rotPercent !== undefined) line.rotPercent = dto.rotPercent;
    if (dto.grossPercent !== undefined) line.grossPercent = dto.grossPercent;
    if (dto.cashDiscPercent !== undefined) line.cashDiscPercent = dto.cashDiscPercent;
    if (dto.cashDiscAmount !== undefined) line.cashDiscAmount = dto.cashDiscAmount;
    if (dto.netAmount !== undefined) line.netAmount = dto.netAmount;
    return line;
  }

  async create(dto: CreatePurchaseOrderDto) {
    const poNo = await this.allocatePoNo();
    const lines: PurchaseOrderLine[] = [];
    for (const l of dto.lines ?? []) {
      lines.push(await this.normalizeDtoLine(l));
    }
    const created = await this.poModel.create({
      poNo,
      branchId: dto.branchId,
      mainDivisionId: dto.mainDivisionId,
      mainLocationId: dto.mainLocationId,
      supplier: dto.supplier,
      poDate: dto.poDate,
      deliveryDate: dto.deliveryDate,
      expiryDate: dto.expiryDate,
      itemDiscAmount: dto.itemDiscAmount,
      cashDiscPercent: dto.cashDiscPercent,
      cashDiscount: dto.cashDiscount,
      taxAmount: dto.taxAmount,
      cgstAmount: dto.cgstAmount,
      sgstAmount: dto.sgstAmount,
      surchargeAmount: dto.surchargeAmount,
      netAmount: dto.netAmount,
      status: (dto.status as PurchaseOrderStatus) ?? 'open',
      lines,
    });
    return await enrichDocWithLineProducts(
      this.productModel,
      created.toObject() as unknown as Record<string, unknown>,
    );
  }

  async findById(id: string) {
    const doc = await this.poModel.findById(id).lean();
    if (!doc) throw new NotFoundException('Purchase order not found');
    return await enrichDocWithLineProducts(this.productModel, doc as unknown as Record<string, unknown>);
  }

  async update(id: string, dto: UpdatePurchaseOrderDto) {
    const set: Record<string, unknown> = {};
    if (dto.branchId !== undefined) set.branchId = dto.branchId;
    if (dto.mainDivisionId !== undefined) set.mainDivisionId = dto.mainDivisionId;
    if (dto.mainLocationId !== undefined) set.mainLocationId = dto.mainLocationId;
    if (dto.supplier !== undefined) set.supplier = dto.supplier;
    if (dto.poDate !== undefined) set.poDate = dto.poDate;
    if (dto.deliveryDate !== undefined) set.deliveryDate = dto.deliveryDate;
    if (dto.expiryDate !== undefined) set.expiryDate = dto.expiryDate;
    if (dto.itemDiscAmount !== undefined) set.itemDiscAmount = dto.itemDiscAmount;
    if (dto.cashDiscPercent !== undefined) set.cashDiscPercent = dto.cashDiscPercent;
    if (dto.cashDiscount !== undefined) set.cashDiscount = dto.cashDiscount;
    if (dto.taxAmount !== undefined) set.taxAmount = dto.taxAmount;
    if (dto.cgstAmount !== undefined) set.cgstAmount = dto.cgstAmount;
    if (dto.sgstAmount !== undefined) set.sgstAmount = dto.sgstAmount;
    if (dto.surchargeAmount !== undefined) set.surchargeAmount = dto.surchargeAmount;
    if (dto.netAmount !== undefined) set.netAmount = dto.netAmount;
    if (dto.status !== undefined) set.status = dto.status;
    if (dto.lines !== undefined) {
      const lines: PurchaseOrderLine[] = [];
      for (const l of dto.lines) {
        lines.push(await this.normalizeDtoLine(l));
      }
      set.lines = lines;
    }
    const doc = await this.poModel.findByIdAndUpdate(id, { $set: set }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Purchase order not found');
    return await enrichDocWithLineProducts(this.productModel, doc as unknown as Record<string, unknown>);
  }

  async list(params: { search?: string; supplierId?: string; status?: string }) {
    const filter: Record<string, unknown> = {};
    if (params.supplierId) filter['supplier.supplierId'] = params.supplierId;
    if (params.status) filter.status = params.status;
    if (params.search) {
      filter.$or = [
        { poNo: { $regex: params.search, $options: 'i' } },
        { 'supplier.name': { $regex: params.search, $options: 'i' } },
      ];
    }
    const rows = await this.poModel.find(filter).sort({ updatedAt: -1 }).limit(200).lean();
    return await attachLineProducts(
      this.productModel,
      rows as Array<{ lines?: Array<Record<string, unknown>> }>,
    );
  }

  async setStatus(id: string, status: PurchaseOrderStatus) {
    const doc = await this.poModel.findByIdAndUpdate(id, { $set: { status } }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Purchase order not found');
    return await enrichDocWithLineProducts(this.productModel, doc as unknown as Record<string, unknown>);
  }

  private async loadProductsForPoLines(lines: PurchaseOrderLine[]): Promise<{
    byId: Map<string, PoRefreshProductSnapshot>;
    bySku: Map<string, PoRefreshProductSnapshot>;
  }> {
    const productIds = new Set<string>();
    const skus = new Set<string>();

    for (const line of lines) {
      if (line.productId != null) {
        const pid = String(line.productId);
        if (isValidObjectIdString(pid)) productIds.add(pid);
      }
      const sku = line.sku?.trim();
      if (sku) skus.add(sku);
    }

    const byId = new Map<string, PoRefreshProductSnapshot>();
    if (productIds.size > 0) {
      const rows = await this.productModel
        .find({ _id: { $in: [...productIds].map((id) => toObjectId(id)) } })
        .select('_id itemName shortName upcEanCode costPrice sellingPrice mrp gstPercent sku')
        .lean();
      for (const row of rows) {
        if (row._id != null) byId.set(String(row._id), row as PoRefreshProductSnapshot);
      }
    }

    const bySku = new Map<string, PoRefreshProductSnapshot>();
    if (skus.size > 0) {
      const rows = await this.productModel
        .find({ sku: { $in: [...skus] } })
        .select('_id itemName shortName upcEanCode costPrice sellingPrice mrp gstPercent sku')
        .lean();
      for (const row of rows) {
        if (typeof row.sku === 'string') bySku.set(row.sku, row as PoRefreshProductSnapshot);
        if (row._id != null) byId.set(String(row._id), row as PoRefreshProductSnapshot);
      }
    }

    return { byId, bySku };
  }

  async refresh(id: string) {
    if (!isValidObjectIdString(id)) {
      throw new BadRequestException('Invalid purchase order id');
    }

    const po = await this.poModel.findById(id).lean();
    if (!po) throw new NotFoundException('Purchase order not found');

    if (!REFRESHABLE_PO_STATUSES.includes(po.status)) {
      throw new BadRequestException(
        `Purchase order refresh is only allowed when status is ${REFRESHABLE_PO_STATUSES.join(' or ')}`,
      );
    }

    const existingLines = po.lines ?? [];
    const { byId, bySku } = await this.loadProductsForPoLines(existingLines);
    const refreshWarnings: string[] = [];
    const refreshedLines: PurchaseOrderLine[] = [];

    for (const line of existingLines) {
      const sku = line.sku?.trim() ?? '';
      const pid = line.productId != null ? String(line.productId) : '';
      const product =
        (pid && byId.get(pid)) ??
        (sku && bySku.get(sku)) ??
        undefined;

      if (!product) {
        refreshWarnings.push(`SKU ${sku || '(empty)'}: product not found`);
        refreshedLines.push({
          ...line,
          recdQty: Math.max(0, line.recdQty ?? 0),
          freeQty: Math.max(0, line.freeQty ?? 0),
        });
        continue;
      }

      refreshedLines.push(refreshPurchaseOrderLine(line, product));
    }

    const header = rollupPurchaseOrderHeaderTotals(
      refreshedLines,
      po.cashDiscPercent,
      po.supplier,
    );

    const doc = await this.poModel
      .findByIdAndUpdate(
        id,
        {
          $set: {
            lines: refreshedLines,
            itemDiscAmount: header.itemDiscAmount,
            surchargeAmount: header.surchargeAmount,
            taxAmount: header.taxAmount,
            cgstAmount: header.cgstAmount,
            sgstAmount: header.sgstAmount,
            cashDiscPercent: header.cashDiscPercent,
            cashDiscount: header.cashDiscount,
            netAmount: header.netAmount,
          },
        },
        { new: true },
      )
      .lean();

    if (!doc) throw new NotFoundException('Purchase order not found');

    const enriched = await enrichDocWithLineProducts(
      this.productModel,
      doc as unknown as Record<string, unknown>,
    );

    if (refreshWarnings.length > 0) {
      return { ...enriched, refreshWarnings };
    }

    return enriched;
  }

  async removePermanent(id: string) {
    if (!isValidObjectIdString(id)) {
      throw new BadRequestException('Invalid purchase order id');
    }

    const po = await this.poModel.findById(id).lean();
    if (!po) throw new NotFoundException('Purchase order not found');

    if (!DELETABLE_PO_STATUSES.includes(po.status)) {
      throw new BadRequestException(
        `Purchase order permanent delete is only allowed when status is ${DELETABLE_PO_STATUSES.join(' or ')}`,
      );
    }

    const linkedGr = await this.grModel.exists({ poId: id }).lean();
    if (linkedGr) {
      throw new ConflictException(
        'Cannot delete purchase order: one or more goods receipts reference this PO',
      );
    }

    await this.poModel.findByIdAndDelete(id);

    return {
      deleted: true,
      id,
      poNo: po.poNo,
    };
  }

  async filter(dto: FilterPurchaseOrderDto) {
    const filter: FilterQuery<PurchaseOrderDocument> = {};

    if (dto.poNo) filter.poNo = dto.poNo;
    if (dto.supplierId) filter['supplier.supplierId'] = dto.supplierId;
    if (dto.branchId) filter.branchId = dto.branchId;
    if (dto.mainDivisionId) filter.mainDivisionId = dto.mainDivisionId;
    if (dto.mainLocationId) filter.mainLocationId = dto.mainLocationId;
    if (dto.status) filter.status = dto.status;

    if (dto.poDateFrom || dto.poDateTo) {
      filter.poDate = {};
      if (dto.poDateFrom) filter.poDate.$gte = dto.poDateFrom;
      if (dto.poDateTo) filter.poDate.$lte = dto.poDateTo;
    }

    if (dto.deliveryDateFrom || dto.deliveryDateTo) {
      filter.deliveryDate = {};
      if (dto.deliveryDateFrom) filter.deliveryDate.$gte = dto.deliveryDateFrom;
      if (dto.deliveryDateTo) filter.deliveryDate.$lte = dto.deliveryDateTo;
    }

    if (dto.netAmountMin !== undefined || dto.netAmountMax !== undefined) {
      filter.netAmount = {};
      if (dto.netAmountMin !== undefined) filter.netAmount.$gte = dto.netAmountMin;
      if (dto.netAmountMax !== undefined) filter.netAmount.$lte = dto.netAmountMax;
    }

    if (dto.search) {
      filter.$or = [
        { poNo: { $regex: dto.search, $options: 'i' } },
        { 'supplier.name': { $regex: dto.search, $options: 'i' } },
      ];
    }

    const page = dto.page ?? 1;
    const limit = dto.limit ?? 20;
    const skip = (page - 1) * limit;
    const sortBy = dto.sortBy ?? 'updatedAt';
    const sortOrder: SortOrder = dto.sortOrder === 'asc' ? 1 : -1;

    const [data, total] = await Promise.all([
      this.poModel
        .find(filter)
        .sort({ [sortBy]: sortOrder })
        .skip(skip)
        .limit(limit)
        .lean(),
      this.poModel.countDocuments(filter),
    ]);

    const enrichedData = await attachLineProducts(
      this.productModel,
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
}
