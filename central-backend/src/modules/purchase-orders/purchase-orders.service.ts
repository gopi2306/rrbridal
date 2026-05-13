import { Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { CreatePurchaseOrderDto } from './dto/create-purchase-order.dto';
import { UpdatePurchaseOrderDto } from './dto/update-purchase-order.dto';
import { PurchaseOrder, PurchaseOrderDocument, PurchaseOrderStatus } from './schemas/purchase-order.schema';

@Injectable()
export class PurchaseOrdersService {
  constructor(@InjectModel(PurchaseOrder.name) private readonly poModel: Model<PurchaseOrderDocument>) {}

  private async nextPoNo() {
    // Simple scaffold: PO-<random 4 digits>. Replace with counter-based sequence later.
    const suffix = Math.floor(1000 + Math.random() * 9000);
    return `PO-${suffix}`;
  }

  async create(dto: CreatePurchaseOrderDto) {
    const poNo = await this.nextPoNo();
    return await this.poModel.create({
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
      surchargeAmount: dto.surchargeAmount,
      netAmount: dto.netAmount,
      status: (dto.status as any) ?? 'open',
      lines: dto.lines ?? [],
    });
  }

  async findById(id: string) {
    const doc = await this.poModel.findById(id).lean();
    if (!doc) throw new NotFoundException('Purchase order not found');
    return doc;
  }

  async update(id: string, dto: UpdatePurchaseOrderDto) {
    const doc = await this.poModel.findByIdAndUpdate(id, { $set: dto }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Purchase order not found');
    return doc;
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
    return await this.poModel.find(filter).sort({ updatedAt: -1 }).limit(200).lean();
  }

  async setStatus(id: string, status: PurchaseOrderStatus) {
    const doc = await this.poModel.findByIdAndUpdate(id, { $set: { status } }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Purchase order not found');
    return doc;
  }
}

