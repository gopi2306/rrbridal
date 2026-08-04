import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { JwtAuthModule } from '../auth/jwt-auth.module';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { StoresModule } from '../stores/stores.module';
import { WhatsAppBroadcastJob, WhatsAppBroadcastJobSchema } from './schemas/whatsapp-broadcast-job.schema';
import { WhatsAppBroadcastService } from './whatsapp-broadcast.service';
import { WhatsAppCloudService } from './whatsapp-cloud.service';
import { WhatsAppController } from './whatsapp.controller';
import { WhatsAppWebhookController } from './whatsapp-webhook.controller';
import { WhatsAppInvoiceService } from './whatsapp-invoice.service';

@Module({
  imports: [
    JwtAuthModule,
    StoresModule,
    MongooseModule.forFeature([{ name: WhatsAppBroadcastJob.name, schema: WhatsAppBroadcastJobSchema }]),
  ],
  controllers: [WhatsAppController, WhatsAppWebhookController],
  providers: [WhatsAppCloudService, WhatsAppInvoiceService, WhatsAppBroadcastService, JwtAuthGuard],
  exports: [WhatsAppInvoiceService, WhatsAppBroadcastService],
})
export class WhatsAppModule {}
