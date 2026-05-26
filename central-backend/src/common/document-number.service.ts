import { Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { IdSequence, IdSequenceDocument } from '../modules/document-numbers/schemas/id-sequence.schema';

export type DocumentNumberConfig = {
  sequenceKey: string;
  prefix: string;
  pad?: number;
  exists?: (value: string) => Promise<boolean>;
  syncFloorFromValues?: () => Promise<number>;
};

@Injectable()
export class DocumentNumberService {
  constructor(@InjectModel(IdSequence.name) private readonly sequenceModel: Model<IdSequenceDocument>) {}

  async allocateNext(config: DocumentNumberConfig): Promise<string> {
    const pad = config.pad ?? 6;
    const pattern = new RegExp(`^${escapeRegex(config.prefix)}(\\d+)$`, 'i');

    if (config.syncFloorFromValues) {
      const floor = await config.syncFloorFromValues();
      if (floor > 0) {
        await this.sequenceModel.updateOne(
          { sequenceKey: config.sequenceKey },
          { $max: { seq: floor } },
          { upsert: true, setDefaultsOnInsert: true },
        );
      }
    }

    for (let attempt = 0; attempt < 5; attempt++) {
      const doc = await this.sequenceModel
        .findOneAndUpdate(
          { sequenceKey: config.sequenceKey },
          { $inc: { seq: 1 }, $setOnInsert: { sequenceKey: config.sequenceKey } },
          { upsert: true, new: true, setDefaultsOnInsert: true },
        )
        .lean();

      const seq = typeof doc?.seq === 'number' ? doc.seq : 1;
      const value = `${config.prefix}${String(seq).padStart(pad, '0')}`;

      if (config.exists) {
        const taken = await config.exists(value);
        if (taken) continue;
      }
      return value;
    }

    return `${config.prefix}${Date.now()}`;
  }

  static parseSequenceNumber(value: string, prefix: string): number | null {
    const m = new RegExp(`^${escapeRegex(prefix)}(\\d+)$`, 'i').exec(value.trim());
    const digits = m?.[1];
    return digits ? parseInt(digits, 10) : null;
  }
}

function escapeRegex(s: string): string {
  return s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
