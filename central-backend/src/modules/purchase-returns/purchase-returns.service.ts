import { Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model, SortOrder } from 'mongoose';
import { CreatePurchaseReturnDto } from './dto/create-purchase-return.dto';
import { FilterPurchaseReturnDto } from './dto/filter-purchase-return.dto';
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

  async filter(dto: FilterPurchaseReturnDto) {
    const filter: FilterQuery<PurchaseReturnDocument> = {};

    if (dto.purchaseReturnNo) filter.purchaseReturnNo = dto.purchaseReturnNo;
    if (dto.supplierId) filter['supplier.supplierId'] = dto.supplierId;
    if (dto.branchId) filter.branchId = dto.branchId;
    if (dto.mainDivisionId) filter.mainDivisionId = dto.mainDivisionId;
    if (dto.mainLocationId) filter.mainLocationId = dto.mainLocationId;
    if (dto.pucOutSlipNo) filter.pucOutSlipNo = dto.pucOutSlipNo;

    if (dto.purchaseReturnDateFrom || dto.purchaseReturnDateTo) {
      filter.purchaseReturnDate = {};
      if (dto.purchaseReturnDateFrom) filter.purchaseReturnDate.$gte = dto.purchaseReturnDateFrom;
      if (dto.purchaseReturnDateTo) filter.purchaseReturnDate.$lte = dto.purchaseReturnDateTo;
    }

    if (dto.netAmountMin !== undefined || dto.netAmountMax !== undefined) {
      filter.netAmount = {};
      if (dto.netAmountMin !== undefined) filter.netAmount.$gte = dto.netAmountMin;
      if (dto.netAmountMax !== undefined) filter.netAmount.$lte = dto.netAmountMax;
    }

    if (dto.search) {
      filter.$or = [
        { purchaseReturnNo: { $regex: dto.search, $options: 'i' } },
        { 'supplier.name': { $regex: dto.search, $options: 'i' } },
        { pucOutSlipNo: { $regex: dto.search, $options: 'i' } },
      ];
    }

    const page = dto.page ?? 1;
    const limit = dto.limit ?? 20;
    const skip = (page - 1) * limit;
    const sortBy = dto.sortBy ?? 'updatedAt';
    const sortOrder: SortOrder = dto.sortOrder === 'asc' ? 1 : -1;

    const [data, total] = await Promise.all([
      this.prModel
        .find(filter)
        .sort({ [sortBy]: sortOrder })
        .skip(skip)
        .limit(limit)
        .lean(),
      this.prModel.countDocuments(filter),
    ]);

    return {
      data,
      total,
      page,
      limit,
      totalPages: Math.ceil(total / limit),
    };
  }
}

