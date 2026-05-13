import { Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { InventoryService } from '../inventory/inventory.service';
import { CreateGoodsReceiptDto } from './dto/create-goods-receipt.dto';
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

