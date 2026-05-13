import { Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model, SortOrder } from 'mongoose';
import { CreatePurchaseOrderDto } from './dto/create-purchase-order.dto';
import { FilterPurchaseOrderDto } from './dto/filter-purchase-order.dto';
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

    return {
      data,
      total,
      page,
      limit,
      totalPages: Math.ceil(total / limit),
    };
  }
}

