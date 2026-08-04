import { BadRequestException, Injectable } from '@nestjs/common';
import { StoresService } from '../stores/stores.service';
import { buildSamplePdf, buildSamplePng } from './whatsapp-sample-attachment';
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
  attachmentMimeType?: string;
};

export type SendInvoiceResult = {
  messageId: string;
  phoneE164: string;
};

export type TestSendInput = {
  storeId?: string;
  customerPhone: string;
  customerName?: string;
  attachment?: Buffer;
  attachmentFilename?: string;
  attachmentMimeType?: string;
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

  /** Test send: if no file uploaded, generates a sample PDF/PNG from store attachmentType. */
  async sendTest(input: TestSendInput): Promise<SendInvoiceResult> {
    const code = await this.resolveStoreCode(input.storeId);
    const store = await this.storesService.findByCode(code);
    const creds = this.storesService.resolveWhatsAppCredentials(
      store.whatsappSettings as Record<string, unknown> | undefined,
    );
    const isDocument = (creds.attachmentType || 'image').toLowerCase() === 'document';

    let attachment = input.attachment;
    let attachmentFilename = input.attachmentFilename;
    let attachmentMimeType = input.attachmentMimeType;

    if (!attachment?.length) {
      if (isDocument) {
        attachment = buildSamplePdf(`Invoice TEST — ${input.customerName?.trim() || 'Test Customer'}`);
        attachmentFilename = 'Invoice_TEST.pdf';
        attachmentMimeType = 'application/pdf';
      } else {
        attachment = buildSamplePng();
        attachmentFilename = 'test-bill.png';
        attachmentMimeType = 'image/png';
      }
    }

    return await this.sendInvoice({
      storeId: code,
      billNo: 'TEST',
      customerName: input.customerName ?? 'Test Customer',
      customerPhone: input.customerPhone,
      payable: 0,
      attachment,
      ...(attachmentFilename ? { attachmentFilename } : {}),
      ...(attachmentMimeType ? { attachmentMimeType } : {}),
    });
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

    const attachmentType = (creds.attachmentType || 'image').toLowerCase();
    const isDocument = attachmentType === 'document';
    this.assertAttachmentMime(isDocument, input.attachmentMimeType, input.attachmentFilename);

    const amountLabel = `₹${Number(input.payable || 0).toLocaleString('en-IN', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    })}`;

    const bodyParams = buildTemplateBodyParams({
      customerName: input.customerName,
      storeName: store.name,
      billNo: input.billNo,
      amountLabel,
      attachmentType,
    });

    if (isDocument) {
      const filename =
        input.attachmentFilename?.trim() || `Invoice_${input.billNo || 'bill'}.pdf`;
      const media = await this.cloud.uploadDocument(
        creds.phoneNumberId,
        creds.accessToken,
        input.attachment,
        filename,
      );
      const sent = await this.cloud.sendTemplateWithDocumentHeader({
        phoneNumberId: creds.phoneNumberId,
        accessToken: creds.accessToken,
        toE164: phoneE164,
        templateName: creds.templateName,
        templateLanguage: creds.templateLanguage,
        bodyParams,
        mediaId: media.id,
        filename,
      });
      return { messageId: sent.messageId, phoneE164 };
    }

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

  private assertAttachmentMime(
    isDocument: boolean,
    mimeType?: string,
    filename?: string,
  ): void {
    const mime = (mimeType ?? '').trim().toLowerCase();
    const name = (filename ?? '').trim().toLowerCase();
    if (isDocument) {
      const ok =
        mime === 'application/pdf' ||
        mime === 'application/octet-stream' ||
        name.endsWith('.pdf') ||
        !mime;
      if (!ok) {
        throw new BadRequestException('Document attachment must be a PDF');
      }
      return;
    }
    const ok =
      mime === 'image/png' ||
      mime.startsWith('image/') ||
      name.endsWith('.png') ||
      !mime;
    if (!ok) {
      throw new BadRequestException('Image attachment must be a PNG');
    }
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
