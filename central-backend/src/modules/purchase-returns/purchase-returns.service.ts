import { Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { CreatePurchaseReturnDto } from './dto/create-purchase-return.dto';
import { UpdatePurchaseReturnDto } from './dto/update-purchase-return.dto';
import { PurchaseReturn, PurchaseReturnDocument } from './schemas/purchase-return.schema';

@Injectable()
export class PurchaseReturnsService {
  constructor(@InjectModel(PurchaseReturn.name) private readonly prModel: Model<PurchaseReturnDocument>) {}

  private async nextPrNo() {
    const suffix = Math.floor(1000 + Math.random() * 9000);
    return `PR-${suffix}`;
  }

  async create(dto: CreatePurchaseReturnDto) {
    const purchaseReturnNo = await this.nextPrNo();
    return await this.prModel.create({
      purchaseReturnNo,
      branchId: dto.branchId,
      mainDivisionId: dto.mainDivisionId,
      mainLocationId: dto.mainLocationId,
      supplier: dto.supplier,
      purchaseReturnDate: dto.purchaseReturnDate,
      pucOutSlipNo: dto.pucOutSlipNo,
      itemDiscAmount: dto.itemDiscAmount,
      cashDiscAmount: dto.cashDiscAmount,
      netAmount: dto.netAmount,
      lines: dto.lines ?? [],
    });
  }

  async findById(id: string) {
    const doc = await this.prModel.findById(id).lean();
    if (!doc) throw new NotFoundException('Purchase return not found');
    return doc;
  }

  async update(id: string, dto: UpdatePurchaseReturnDto) {
    const doc = await this.prModel.findByIdAndUpdate(id, { $set: dto }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Purchase return not found');
    return doc;
  }

  async list(params: { search?: string; supplierId?: string }) {
    const filter: Record<string, unknown> = {};
    if (params.supplierId) filter['supplier.supplierId'] = params.supplierId;
    if (params.search) {
      filter.$or = [
        { purchaseReturnNo: { $regex: params.search, $options: 'i' } },
        { 'supplier.name': { $regex: params.search, $options: 'i' } },
      ];
    }
    return await this.prModel.find(filter).sort({ updatedAt: -1 }).limit(200).lean();
  }
}

