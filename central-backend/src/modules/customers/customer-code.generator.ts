import { Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { DocumentNumberService } from '../../common/document-number.service';
import { DocumentNumberAllocatorService } from '../document-numbers/document-number-allocator.service';
import { DocumentNumberConfigService } from '../document-numbers/document-number-config.service';
import { Customer, CustomerDocument } from './schemas/customer.schema';

@Injectable()
export class CustomerCodeGenerator {
  constructor(
    @InjectModel(Customer.name) private readonly customerModel: Model<CustomerDocument>,
    private readonly allocator: DocumentNumberAllocatorService,
    private readonly configService: DocumentNumberConfigService,
  ) {}

  async allocateNextAsync(): Promise<string> {
    const config = await this.configService.getByKey('customer_code');
    const prefix = config.prefix;

    return this.allocator.allocate('customer_code', {
      exists: async (v) => !!(await this.customerModel.exists({ customerCode: v }).lean()),
      syncFloorFromValues: () => this.maxSequenceForPrefix(prefix),
    });
  }

  private async maxSequenceForPrefix(prefix: string): Promise<number> {
    const escaped = prefix.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const regex = new RegExp(`^${escaped}\\d+$`, 'i');
    const rows = await this.customerModel.find({ customerCode: regex }).select('customerCode').lean();

    let max = 0;
    for (const row of rows) {
      if (typeof row.customerCode !== 'string') continue;
      const n = DocumentNumberService.parseSequenceNumber(row.customerCode, prefix);
      if (n !== null && n > max) max = n;
    }
    return max;
  }
}
