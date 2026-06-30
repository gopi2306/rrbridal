import { Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { DocumentNumberService } from '../../common/document-number.service';
import { DocumentNumberAllocatorService } from '../document-numbers/document-number-allocator.service';
import { DocumentNumberConfigService } from '../document-numbers/document-number-config.service';
import { Salesman, SalesmanDocument } from './schemas/salesman.schema';

@Injectable()
export class SalesmanCodeGenerator {
  constructor(
    @InjectModel(Salesman.name) private readonly salesmanModel: Model<SalesmanDocument>,
    private readonly allocator: DocumentNumberAllocatorService,
    private readonly configService: DocumentNumberConfigService,
  ) {}

  async allocateNextAsync(storeId: string): Promise<string> {
    const trimmedStoreId = storeId.trim();
    const config = await this.configService.getByKey('salesman_code');
    const prefix = config.prefix;

    return this.allocator.allocate('salesman_code', {
      exists: async (v) =>
        !!(await this.salesmanModel.exists({ storeId: trimmedStoreId, salesmanCode: v }).lean()),
      syncFloorFromValues: () => this.maxSequenceForStorePrefix(trimmedStoreId, prefix),
    });
  }

  private async maxSequenceForStorePrefix(storeId: string, prefix: string): Promise<number> {
    const escaped = prefix.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const regex = new RegExp(`^${escaped}\\d+$`, 'i');
    const rows = await this.salesmanModel
      .find({ storeId, salesmanCode: regex })
      .select('salesmanCode')
      .lean();

    let max = 0;
    for (const row of rows) {
      if (typeof row.salesmanCode !== 'string') continue;
      const n = DocumentNumberService.parseSequenceNumber(row.salesmanCode, prefix);
      if (n !== null && n > max) max = n;
    }
    return max;
  }
}
