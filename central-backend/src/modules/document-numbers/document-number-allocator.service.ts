import { Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { DocumentNumberService } from '../../common/document-number.service';
import { DocumentNumberConfigKey } from './document-number-config-keys';
import { DocumentNumberConfigService } from './document-number-config.service';
import { IdSequence, IdSequenceDocument } from './schemas/id-sequence.schema';

export type AllocateDocumentNumberOptions = {
  exists?: (value: string) => Promise<boolean>;
  syncFloorFromValues?: () => Promise<number>;
};

@Injectable()
export class DocumentNumberAllocatorService {
  constructor(
    private readonly configService: DocumentNumberConfigService,
    private readonly documentNumbers: DocumentNumberService,
    @InjectModel(IdSequence.name) private readonly sequenceModel: Model<IdSequenceDocument>,
  ) {}

  async allocate(
    configKey: DocumentNumberConfigKey,
    options: AllocateDocumentNumberOptions = {},
  ): Promise<string> {
    const config = await this.configService.getByKey(configKey);
    this.configService.assertStartFromFitsPad(config.startFrom, config.padLength);

    await this.sequenceModel.updateOne(
      { sequenceKey: configKey },
      { $max: { seq: config.startFrom - 1 }, $setOnInsert: { sequenceKey: configKey } },
      { upsert: true, setDefaultsOnInsert: true },
    );

    const allocConfig: Parameters<DocumentNumberService['allocateNext']>[0] = {
      sequenceKey: configKey,
      prefix: config.prefix,
      pad: config.padLength,
    };
    if (options.exists) allocConfig.exists = options.exists;
    if (options.syncFloorFromValues) allocConfig.syncFloorFromValues = options.syncFloorFromValues;
    return this.documentNumbers.allocateNext(allocConfig);
  }
}
