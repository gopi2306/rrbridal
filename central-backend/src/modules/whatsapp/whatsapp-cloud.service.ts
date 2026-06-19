import { BadRequestException, Injectable, Logger } from '@nestjs/common';

type UploadResult = { id: string };

type SendResult = { messageId: string };

@Injectable()
export class WhatsAppCloudService {
  private readonly logger = new Logger(WhatsAppCloudService.name);

  private graphVersion(): string {
    return process.env.WHATSAPP_GRAPH_VERSION?.trim() || 'v21.0';
  }

  async uploadImage(
    phoneNumberId: string,
    accessToken: string,
    buffer: Buffer,
    filename: string,
  ): Promise<UploadResult> {
    const form = new FormData();
    form.append('messaging_product', 'whatsapp');
    form.append('type', 'image/png');
    form.append('file', new Blob([new Uint8Array(buffer)], { type: 'image/png' }), filename || 'bill.png');

    const url = `https://graph.facebook.com/${this.graphVersion()}/${phoneNumberId}/media`;
    const res = await fetch(url, {
      method: 'POST',
      headers: { Authorization: `Bearer ${accessToken}` },
      body: form,
    });
    const body = (await res.json()) as { id?: string; error?: { message?: string } };
    if (!res.ok) {
      const msg = body.error?.message ?? `Media upload failed (${res.status})`;
      this.logger.warn(`WhatsApp media upload failed: ${msg}`);
      throw new BadRequestException(msg);
    }
    if (!body.id) throw new BadRequestException('WhatsApp media upload returned no id');
    return { id: body.id };
  }

  async sendTemplateWithImageHeader(input: {
    phoneNumberId: string;
    accessToken: string;
    toE164: string;
    templateName: string;
    templateLanguage: string;
    bodyParams: string[];
    mediaId: string;
  }): Promise<SendResult> {
    const payload = {
      messaging_product: 'whatsapp',
      to: input.toE164,
      type: 'template',
      template: {
        name: input.templateName,
        language: { code: input.templateLanguage },
        components: [
          {
            type: 'header',
            parameters: [{ type: 'image', image: { id: input.mediaId } }],
          },
          {
            type: 'body',
            parameters: input.bodyParams.map((text) => ({ type: 'text', text })),
          },
        ],
      },
    };

    const url = `https://graph.facebook.com/${this.graphVersion()}/${input.phoneNumberId}/messages`;
    const res = await fetch(url, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${input.accessToken}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(payload),
    });
    const body = (await res.json()) as {
      messages?: Array<{ id?: string }>;
      error?: { message?: string };
    };
    if (!res.ok) {
      const msg = body.error?.message ?? `WhatsApp send failed (${res.status})`;
      this.logger.warn(`WhatsApp send failed: ${msg}`);
      throw new BadRequestException(msg);
    }
    const messageId = body.messages?.[0]?.id;
    if (!messageId) throw new BadRequestException('WhatsApp send returned no message id');
    return { messageId };
  }
}
