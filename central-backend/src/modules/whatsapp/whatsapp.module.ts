import { Module } from '@nestjs/common';
import { JwtAuthModule } from '../auth/jwt-auth.module';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { StoresModule } from '../stores/stores.module';
import { WhatsAppCloudService } from './whatsapp-cloud.service';
import { WhatsAppController } from './whatsapp.controller';
import { WhatsAppInvoiceService } from './whatsapp-invoice.service';

@Module({
  imports: [JwtAuthModule, StoresModule],
  controllers: [WhatsAppController],
  providers: [WhatsAppCloudService, WhatsAppInvoiceService, JwtAuthGuard],
  exports: [WhatsAppInvoiceService],
})
export class WhatsAppModule {}
