import { BadRequestException, Injectable } from '@nestjs/common';
import { StoresService } from '../stores/stores.service';
import { buildTemplateBodyParams, normalizeWhatsAppPhone } from './whatsapp.util';
import { WhatsAppCloudService } from './whatsapp-cloud.service';

export type SendInvoiceInput = {
  storeId: string;
  billNo: string;
  customerName: string;
  customerPhone: string;
  payable: number;
  attachment: Buffer;
  attachmentFilename?: string;
};

export type SendInvoiceResult = {
  messageId: string;
  phoneE164: string;
};

@Injectable()
export class WhatsAppInvoiceService {
  constructor(
    private readonly storesService: StoresService,
    private readonly cloud: WhatsAppCloudService,
  ) {}

  async getSettings(storeId?: string) {
    const code = await this.resolveStoreCode(storeId);
    return await this.storesService.getWhatsAppSettingsPublic(code);
  }

  async sendInvoice(input: SendInvoiceInput): Promise<SendInvoiceResult> {
    const code = await this.resolveStoreCode(input.storeId);
    const store = await this.storesService.findByCode(code);
    const creds = this.storesService.resolveWhatsAppCredentials(
      store.whatsappSettings as Record<string, unknown> | undefined,
    );

    if (!creds.enabled) throw new BadRequestException('WhatsApp is disabled for this store');
    if (!creds.phoneNumberId || !creds.accessToken || !creds.templateName) {
      throw new BadRequestException('WhatsApp is not fully configured for this store');
    }

    const phoneE164 = normalizeWhatsAppPhone(input.customerPhone, creds.defaultCountryCode);
    if (!phoneE164 || phoneE164.length < 11) {
      throw new BadRequestException('Valid customer phone is required');
    }
    if (!input.attachment?.length) {
      throw new BadRequestException('Invoice attachment is required');
    }

    const amountLabel = `₹${Number(input.payable || 0).toLocaleString('en-IN', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    })}`;

    const bodyParams = buildTemplateBodyParams({
      customerName: input.customerName,
      storeName: store.name,
      billNo: input.billNo,
      amountLabel,
    });

    const filename = input.attachmentFilename?.trim() || `${input.billNo}.png`;
    const media = await this.cloud.uploadImage(
      creds.phoneNumberId,
      creds.accessToken,
      input.attachment,
      filename,
    );

    const sent = await this.cloud.sendTemplateWithImageHeader({
      phoneNumberId: creds.phoneNumberId,
      accessToken: creds.accessToken,
      toE164: phoneE164,
      templateName: creds.templateName,
      templateLanguage: creds.templateLanguage,
      bodyParams,
      mediaId: media.id,
    });

    return { messageId: sent.messageId, phoneE164 };
  }

  private async resolveStoreCode(storeId?: string): Promise<string> {
    const code = storeId?.trim().toLowerCase();
    if (code) return code;
    const stores = await this.storesService.findAll();
    const active = stores.find((s) => s.status === 'active');
    if (!active) throw new BadRequestException('No active store found');
    return active.code;
  }
}
