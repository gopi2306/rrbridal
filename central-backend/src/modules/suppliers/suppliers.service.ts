import { Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model, SortOrder } from 'mongoose';
import { CreateSupplierDto } from './dto/create-supplier.dto';
import { FilterSupplierDto } from './dto/filter-supplier.dto';
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

  async remove(id: string) {
    const doc = await this.supplierModel.findByIdAndDelete(id).lean();
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

  async filter(dto: FilterSupplierDto) {
    const filter: FilterQuery<SupplierDocument> = {};

    const exactMatchFields = [
      'name',
      'gstNumber',
      'gstStateCode',
      'gstRegistrationType',
      'panNumber',
      'mobileNo',
      'emailId',
      'contactPerson',
      'country',
      'state',
      'city',
      'pin',
      'businessRelatedType',
    ] as const;

    for (const field of exactMatchFields) {
      if (dto[field] !== undefined && dto[field] !== null) {
        filter[field] = dto[field];
      }
    }

    if (dto.isActive !== undefined && dto.isActive !== null) {
      filter.isActive = dto.isActive;
    }

    if (dto.isSupplier !== undefined && dto.isSupplier !== null) {
      filter.isSupplier = dto.isSupplier;
    }

    if (dto.search) {
      filter.$or = [
        { name: { $regex: dto.search, $options: 'i' } },
        { mobileNo: { $regex: dto.search, $options: 'i' } },
        { emailId: { $regex: dto.search, $options: 'i' } },
        { gstNumber: { $regex: dto.search, $options: 'i' } },
        { panNumber: { $regex: dto.search, $options: 'i' } },
        { contactPerson: { $regex: dto.search, $options: 'i' } },
      ];
    }

    const page = dto.page ?? 1;
    const limit = dto.limit ?? 20;
    const skip = (page - 1) * limit;
    const sortBy = dto.sortBy ?? 'updatedAt';
    const sortOrder: SortOrder = dto.sortOrder === 'asc' ? 1 : -1;

    const [data, total] = await Promise.all([
      this.supplierModel
        .find(filter)
        .sort({ [sortBy]: sortOrder })
        .skip(skip)
        .limit(limit)
        .lean(),
      this.supplierModel.countDocuments(filter),
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
