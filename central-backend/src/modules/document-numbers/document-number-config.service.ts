import { BadRequestException, Injectable, NotFoundException, OnModuleInit } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import {
  DOCUMENT_NUMBER_CONFIG_DEFAULTS,
  DocumentNumberConfigKey,
  isDocumentNumberConfigKey,
} from './document-number-config-keys';
import { PatchDocumentNumberConfigDto } from './dto/patch-document-number-config.dto';
import {
  DocumentNumberConfig,
  DocumentNumberConfigDocument,
} from './schemas/document-number-config.schema';
import { IdSequence, IdSequenceDocument } from './schemas/id-sequence.schema';

@Injectable()
export class DocumentNumberConfigService implements OnModuleInit {
  constructor(
    @InjectModel(DocumentNumberConfig.name)
    private readonly configModel: Model<DocumentNumberConfigDocument>,
    @InjectModel(IdSequence.name) private readonly sequenceModel: Model<IdSequenceDocument>,
  ) {}

  async onModuleInit() {
    await this.ensureDefaults();
    await this.migrateLegacyIdSequences();
  }

  /** Legacy rows used string `_id` (e.g. product_sku); re-key to `sequenceKey`. */
  private async migrateLegacyIdSequences(): Promise<void> {
    const coll = this.sequenceModel.collection;
    const legacy = await coll.find({ sequenceKey: { $exists: false } }).toArray();
    for (const doc of legacy) {
      const key = typeof doc._id === 'string' ? doc._id : undefined;
      if (!key) continue;
      const seq = typeof doc.seq === 'number' ? doc.seq : 0;
      await this.sequenceModel.updateOne(
        { sequenceKey: key },
        { $max: { seq }, $setOnInsert: { sequenceKey: key } },
        { upsert: true, setDefaultsOnInsert: true },
      );
      await coll.deleteOne({ _id: doc._id });
    }
  }

  async ensureDefaults(): Promise<void> {
    for (const def of DOCUMENT_NUMBER_CONFIG_DEFAULTS) {
      await this.configModel.updateOne(
        { configKey: def.configKey },
        {
          $setOnInsert: {
            configKey: def.configKey,
            prefix: def.prefix,
            padLength: def.padLength,
            startFrom: def.startFrom,
            label: def.label,
            description: def.description,
          },
        },
        { upsert: true },
      );
    }
  }

  async listAll() {
    await this.ensureDefaults();
    return await this.configModel.find().sort({ configKey: 1 }).lean();
  }

  async getByKey(configKey: DocumentNumberConfigKey) {
    await this.ensureDefaults();
    const doc = await this.configModel.findOne({ configKey }).lean();
    if (!doc) throw new NotFoundException(`Document number config '${configKey}' not found`);
    return doc;
  }

  async patch(configKey: string, dto: PatchDocumentNumberConfigDto) {
    if (!isDocumentNumberConfigKey(configKey)) {
      throw new NotFoundException(`Unknown document number config key '${configKey}'`);
    }
    await this.ensureDefaults();
    const existing = await this.configModel.findOne({ configKey }).lean();
    if (!existing) throw new NotFoundException(`Document number config '${configKey}' not found`);

    const prefix = dto.prefix != null ? String(dto.prefix).trim() : existing.prefix;
    const padLength = dto.padLength ?? existing.padLength;
    const startFrom = dto.startFrom ?? existing.startFrom;

    this.assertStartFromFitsPad(startFrom, padLength);

    if (dto.startFrom != null && dto.startFrom < existing.startFrom) {
      throw new BadRequestException(
        `startFrom cannot be lowered below ${existing.startFrom} (omit force reset in v1)`,
      );
    }

    if (dto.startFrom != null && dto.startFrom > existing.startFrom) {
      const seqDoc = await this.sequenceModel.findOne({ sequenceKey: configKey }).lean();
      const currentSeq = typeof seqDoc?.seq === 'number' ? seqDoc.seq : 0;
      if (dto.startFrom <= currentSeq) {
        throw new BadRequestException(
          `startFrom must be greater than the current sequence (${currentSeq}); next number would be ${currentSeq + 1}`,
        );
      }
    }

    const updated = await this.configModel
      .findOneAndUpdate(
        { configKey },
        {
          ...(dto.prefix != null ? { prefix } : {}),
          ...(dto.padLength != null ? { padLength } : {}),
          ...(dto.startFrom != null ? { startFrom } : {}),
          ...(dto.label != null ? { label: dto.label } : {}),
          ...(dto.description != null ? { description: dto.description } : {}),
        },
        { new: true },
      )
      .lean();

    if (dto.startFrom != null) {
      await this.applyStartFromFloor(configKey, dto.startFrom);
    }

    return updated;
  }

  async applyStartFromFloor(configKey: DocumentNumberConfigKey, startFrom: number): Promise<void> {
    if (startFrom < 1) return;
    await this.sequenceModel.updateOne(
      { sequenceKey: configKey },
      { $max: { seq: startFrom - 1 }, $setOnInsert: { sequenceKey: configKey } },
      { upsert: true, setDefaultsOnInsert: true },
    );
  }

  assertStartFromFitsPad(startFrom: number, padLength: number): void {
    const digits = String(startFrom).length;
    if (digits > padLength) {
      throw new BadRequestException(
        `startFrom (${startFrom}) requires at least ${digits} pad digits; increase padLength or lower startFrom`,
      );
    }
  }
}
