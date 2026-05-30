import { Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model, SortOrder } from 'mongoose';
import { CreateCustomerDto } from './dto/create-customer.dto';
import { FilterCustomerDto } from './dto/filter-customer.dto';
import { UpdateCustomerDto } from './dto/update-customer.dto';
import { Customer, CustomerDocument } from './schemas/customer.schema';

const FILTER_SORT_FIELDS = new Set([
  'name',
  'phone',
  'email',
  'customerCode',
  'city',
  'state',
  'createdAt',
  'updatedAt',
]);

@Injectable()
export class CustomersService {
  constructor(@InjectModel(Customer.name) private readonly customerModel: Model<CustomerDocument>) {}

  async create(dto: CreateCustomerDto) {
    return await this.customerModel.create({ ...dto, isActive: dto.isActive ?? true });
  }

  async findByCustomerCode(code: string) {
    const trimmed = code?.trim();
    if (!trimmed) return null;
    return await this.customerModel.findOne({ customerCode: trimmed }).lean();
  }

  async findByPhone(phone: string) {
    const trimmed = phone?.trim();
    if (!trimmed) return null;
    const docs = await this.customerModel.find({ phone: trimmed }).limit(2).lean();
    if (docs.length === 0) return null;
    if (docs.length > 1) {
      throw new Error(`Multiple customers match phone "${trimmed}"`);
    }
    return docs[0];
  }

  /** Create or update for import: customerCode first, then phone, else create. */
  async upsertForImport(dto: CreateCustomerDto): Promise<{ created: boolean; customer: unknown }> {
    const code = dto.customerCode?.trim();
    if (code) {
      const existing = await this.findByCustomerCode(code);
      if (existing) {
        const { customerCode: _code, ...rest } = dto;
        const customer = await this.update(String(existing._id), { ...rest, customerCode: code });
        return { created: false, customer };
      }
    }

    const phone = dto.phone?.trim();
    if (phone) {
      const existing = await this.findByPhone(phone);
      if (existing) {
        const customer = await this.update(String(existing._id), dto);
        return { created: false, customer };
      }
    }

    const customer = await this.create(dto);
    return { created: true, customer };
  }

  async findExistingForImport(dto: CreateCustomerDto) {
    const code = dto.customerCode?.trim();
    if (code) {
      const byCode = await this.findByCustomerCode(code);
      if (byCode) return byCode;
    }
    const phone = dto.phone?.trim();
    if (phone) return await this.findByPhone(phone);
    return null;
  }

  async findById(id: string) {
    const doc = await this.customerModel.findById(id).lean();
    if (!doc) throw new NotFoundException('Customer not found');
    return doc;
  }

  async update(id: string, dto: UpdateCustomerDto) {
    const doc = await this.customerModel.findByIdAndUpdate(id, { $set: dto }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Customer not found');
    return doc;
  }

  async list(params: { search?: string; phone?: string }) {
    const filter: FilterQuery<CustomerDocument> = {};
    if (params.phone) filter.phone = params.phone;
    if (params.search) {
      filter.$or = [
        { name: { $regex: params.search, $options: 'i' } },
        { phone: { $regex: params.search, $options: 'i' } },
        { email: { $regex: params.search, $options: 'i' } },
        { customerCode: { $regex: params.search, $options: 'i' } },
      ];
    }
    return await this.customerModel.find(filter).sort({ updatedAt: -1 }).limit(200).lean();
  }

  async filter(dto: FilterCustomerDto) {
    const filter: FilterQuery<CustomerDocument> = {};

    const exactMatchFields = [
      'customerCode',
      'name',
      'phone',
      'email',
      'gstin',
      'city',
      'state',
      'pincode',
    ] as const;

    for (const field of exactMatchFields) {
      if (dto[field] !== undefined && dto[field] !== null && dto[field] !== '') {
        filter[field] = dto[field];
      }
    }

    if (dto.isActive !== undefined && dto.isActive !== null) {
      filter.isActive = dto.isActive;
    }

    if (dto.search) {
      filter.$or = [
        { name: { $regex: dto.search, $options: 'i' } },
        { phone: { $regex: dto.search, $options: 'i' } },
        { email: { $regex: dto.search, $options: 'i' } },
        { customerCode: { $regex: dto.search, $options: 'i' } },
        { gstin: { $regex: dto.search, $options: 'i' } },
      ];
    }

    const page = dto.page ?? 1;
    const limit = dto.limit ?? 20;
    const skip = (page - 1) * limit;
    const sortBy = FILTER_SORT_FIELDS.has(dto.sortBy ?? '') ? dto.sortBy! : 'updatedAt';
    const sortOrder: SortOrder = dto.sortOrder === 'asc' ? 1 : -1;

    const [data, total] = await Promise.all([
      this.customerModel
        .find(filter)
        .sort({ [sortBy]: sortOrder })
        .skip(skip)
        .limit(limit)
        .lean(),
      this.customerModel.countDocuments(filter),
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
