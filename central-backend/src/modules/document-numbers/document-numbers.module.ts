import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { RolesGuard } from '../../common/guards/roles.guard';
import { DocumentNumberService } from '../../common/document-number.service';
import { JwtAuthModule } from '../auth/jwt-auth.module';
import { AdminDocumentNumberConfigController } from './admin-document-number-config.controller';
import { DocumentNumberAllocatorService } from './document-number-allocator.service';
import { DocumentNumberConfigService } from './document-number-config.service';
import {
  DocumentNumberConfig,
  DocumentNumberConfigSchema,
} from './schemas/document-number-config.schema';
import { IdSequence, IdSequenceSchema } from './schemas/id-sequence.schema';

@Module({
  imports: [
    JwtAuthModule,
    MongooseModule.forFeature([
      { name: DocumentNumberConfig.name, schema: DocumentNumberConfigSchema },
      { name: IdSequence.name, schema: IdSequenceSchema },
    ]),
  ],
  controllers: [AdminDocumentNumberConfigController],
  providers: [
    DocumentNumberConfigService,
    DocumentNumberAllocatorService,
    DocumentNumberService,
    JwtAuthGuard,
    RolesGuard,
  ],
  exports: [DocumentNumberConfigService, DocumentNumberAllocatorService, DocumentNumberService],
})
export class DocumentNumbersModule {}
