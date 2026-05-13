import { Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model } from 'mongoose';
import { CreateSupplierDto } from './dto/create-supplier.dto';
import { UpdateSupplierDto } from './dto/update-supplier.dto';
import { Supplier, SupplierDocument } from './schemas/supplier.schema';

@Injectable()
export class SuppliersService {
  constructor(@InjectModel(Supplier.name) private readonly supplierModel: Model<SupplierDocument>) {}

  async create(dto: CreateSupplierDto) {
    return await this.supplierModel.create({
      ...dto,
      isActive: dto.isActive ?? true,
      isSupplier: dto.isSupplier ?? true,
    });
  }

  async findById(id: string) {
    const doc = await this.supplierModel.findById(id).lean();
    if (!doc) throw new NotFoundException('Supplier not found');
    return doc;
  }

  async update(id: string, dto: UpdateSupplierDto) {
    const doc = await this.supplierModel.findByIdAndUpdate(id, { $set: dto }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Supplier not found');
    return doc;
  }

  async list(params: { search?: string; mobileNo?: string }) {
    const filter: FilterQuery<SupplierDocument> = {};
    if (params.mobileNo) filter.mobileNo = params.mobileNo;
    if (params.search) {
      filter.$or = [
        { name: { $regex: params.search, $options: 'i' } },
        { mobileNo: { $regex: params.search, $options: 'i' } },
        { emailId: { $regex: params.search, $options: 'i' } },
        { gstNumber: { $regex: params.search, $options: 'i' } },
        { panNumber: { $regex: params.search, $options: 'i' } },
        { contactPerson: { $regex: params.search, $options: 'i' } },
      ];
    }
    return await this.supplierModel.find(filter).sort({ updatedAt: -1 }).limit(200).lean();
  }
}
