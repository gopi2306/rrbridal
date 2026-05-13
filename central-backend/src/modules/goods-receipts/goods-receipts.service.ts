import { Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model, SortOrder } from 'mongoose';
import { InventoryService } from '../inventory/inventory.service';
import { CreateGoodsReceiptDto } from './dto/create-goods-receipt.dto';
import { FilterGoodsReceiptDto } from './dto/filter-goods-receipt.dto';
import { UpdateGoodsReceiptDto } from './dto/update-goods-receipt.dto';
import { GoodsReceipt, GoodsReceiptDocument } from './schemas/goods-receipt.schema';

@Injectable()
export class GoodsReceiptsService {
  constructor(
    @InjectModel(GoodsReceipt.name) private readonly grModel: Model<GoodsReceiptDocument>,
    private readonly inventoryService: InventoryService,
  ) {}

  private async nextReceiptNo() {
    const suffix = Math.floor(10 + Math.random() * 90);
    return `RCV-${suffix}`;
  }

  async create(dto: CreateGoodsReceiptDto) {
    const receiptNo = await this.nextReceiptNo();
    return await this.grModel.create({
      receiptNo,
      poId: dto.poId,
      poNo: dto.poNo,
      supplier: dto.supplier,
      invoiceNo: dto.invoiceNo,
      invoiceDate: dto.invoiceDate,
      remarks: dto.remarks,
      status: (dto.status as any) ?? 'draft',
      lines: dto.lines ?? [],
    });
  }

  async findById(id: string) {
    const doc = await this.grModel.findById(id).lean();
    if (!doc) throw new NotFoundException('Goods receipt not found');
    return doc;
  }

  async update(id: string, dto: UpdateGoodsReceiptDto) {
    const doc = await this.grModel.findByIdAndUpdate(id, { $set: dto }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Goods receipt not found');
    return doc;
  }

  async list(params: { search?: string; poNo?: string; status?: string }) {
    const filter: Record<string, unknown> = {};
    if (params.poNo) filter.poNo = params.poNo;
    if (params.status) filter.status = params.status;
    if (params.search) {
      filter.$or = [
        { receiptNo: { $regex: params.search, $options: 'i' } },
        { poNo: { $regex: params.search, $options: 'i' } },
        { invoiceNo: { $regex: params.search, $options: 'i' } },
        { 'supplier.name': { $regex: params.search, $options: 'i' } },
      ];
    }
    return await this.grModel.find(filter).sort({ updatedAt: -1 }).limit(200).lean();
  }

  async filter(dto: FilterGoodsReceiptDto) {
    const filter: FilterQuery<GoodsReceiptDocument> = {};

    if (dto.receiptNo) filter.receiptNo = dto.receiptNo;
    if (dto.poId) filter.poId = dto.poId;
    if (dto.poNo) filter.poNo = dto.poNo;
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

    return {
      data,
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
    return doc.toObject();
  }
}

