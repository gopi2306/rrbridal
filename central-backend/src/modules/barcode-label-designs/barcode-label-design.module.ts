import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { JwtAuthModule } from '../auth/jwt-auth.module';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { AdminBarcodeLabelDesignController } from './admin-barcode-label-design.controller';
import { BarcodeLabelDesignController } from './barcode-label-design.controller';
import { BarcodeLabelDesignService } from './barcode-label-design.service';
import {
  BarcodeLabelDesign,
  BarcodeLabelDesignSchema,
} from './schemas/barcode-label-design.schema';
import {
  BarcodePrinterProfile,
  BarcodePrinterProfileSchema,
} from './schemas/barcode-printer-profile.schema';

@Module({
  imports: [
    JwtAuthModule,
    MongooseModule.forFeature([
      { name: BarcodeLabelDesign.name, schema: BarcodeLabelDesignSchema },
      { name: BarcodePrinterProfile.name, schema: BarcodePrinterProfileSchema },
    ]),
  ],
  controllers: [AdminBarcodeLabelDesignController, BarcodeLabelDesignController],
  providers: [BarcodeLabelDesignService, JwtAuthGuard],
  exports: [BarcodeLabelDesignService],
})
export class BarcodeLabelDesignsModule {}
