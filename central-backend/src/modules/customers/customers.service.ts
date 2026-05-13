import { Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model } from 'mongoose';
import { CreateCustomerDto } from './dto/create-customer.dto';
import { UpdateCustomerDto } from './dto/update-customer.dto';
import { Customer, CustomerDocument } from './schemas/customer.schema';

@Injectable()
export class CustomersService {
  constructor(@InjectModel(Customer.name) private readonly customerModel: Model<CustomerDocument>) {}

  async create(dto: CreateCustomerDto) {
    return await this.customerModel.create({ ...dto, isActive: dto.isActive ?? true });
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
}

