import { BadRequestException, Injectable, Logger } from '@nestjs/common';

type UploadResult = { id: string };

type SendResult = { messageId: string };

@Injectable()
export class WhatsAppCloudService {
  private readonly logger = new Logger(WhatsAppCloudService.name);

  private graphVersion(): string {
    return process.env.WHATSAPP_GRAPH_VERSION?.trim() || 'v25.0';
  }

  /** Strip accidental "Bearer " prefix and whitespace from stored tokens. */
  private normalizeToken(accessToken: string): string {
    return accessToken.trim().replace(/^Bearer\s+/i, '').trim();
  }

  async uploadImage(
    phoneNumberId: string,
    accessToken: string,
    buffer: Buffer,
    filename: string,
  ): Promise<UploadResult> {
    return await this.uploadMedia(phoneNumberId, accessToken, buffer, filename, 'image/png');
  }

  async uploadDocument(
    phoneNumberId: string,
    accessToken: string,
    buffer: Buffer,
    filename: string,
  ): Promise<UploadResult> {
    return await this.uploadMedia(phoneNumberId, accessToken, buffer, filename, 'application/pdf');
  }

  private async uploadMedia(
    phoneNumberId: string,
    accessToken: string,
    buffer: Buffer,
    filename: string,
    mimeType: string,
  ): Promise<UploadResult> {
    const form = new FormData();
    form.append('messaging_product', 'whatsapp');
    form.append('type', mimeType);
    form.append(
      'file',
      new Blob([new Uint8Array(buffer)], { type: mimeType }),
      filename || (mimeType === 'application/pdf' ? 'bill.pdf' : 'bill.png'),
    );

    const id = phoneNumberId.trim();
    const token = this.normalizeToken(accessToken);
    const url = `https://graph.facebook.com/${this.graphVersion()}/${id}/media`;
    const res = await fetch(url, {
      method: 'POST',
      headers: { Authorization: `Bearer ${token}` },
      body: form,
    });
    const body = (await res.json()) as { id?: string; error?: { message?: string; code?: number } };
    if (!res.ok) {
      throw new BadRequestException(this.formatMetaError(body.error, id, 'media upload'));
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
    return await this.sendTemplate({
      ...input,
      headerParameters: [{ type: 'image', image: { id: input.mediaId } }],
    });
  }

  async sendTemplateWithDocumentHeader(input: {
    phoneNumberId: string;
    accessToken: string;
    toE164: string;
    templateName: string;
    templateLanguage: string;
    bodyParams: string[];
    mediaId: string;
    filename?: string;
  }): Promise<SendResult> {
    const document: { id: string; filename?: string } = { id: input.mediaId };
    const filename = input.filename?.trim();
    if (filename) document.filename = filename;
    return await this.sendTemplate({
      ...input,
      headerParameters: [{ type: 'document', document }],
    });
  }

  /** Marketing / promo template with body variables only (no header component). */
  async sendTemplateBodyOnly(input: {
    phoneNumberId: string;
    accessToken: string;
    toE164: string;
    templateName: string;
    templateLanguage: string;
    bodyParams: string[];
    urlButtonParams?: Array<{ index: string; text: string }>;
  }): Promise<SendResult> {
    return await this.sendTemplate({
      phoneNumberId: input.phoneNumberId,
      accessToken: input.accessToken,
      toE164: input.toE164,
      templateName: input.templateName,
      templateLanguage: input.templateLanguage,
      bodyParams: input.bodyParams,
      headerParameters: null,
      ...(input.urlButtonParams ? { urlButtonParams: input.urlButtonParams } : {}),
    });
  }

  /** Promo marketing template — body/button shape from store promoBodyParamMode / promoHasUrlButton. */
  async sendPromoBroadcastTemplate(input: {
    phoneNumberId: string;
    accessToken: string;
    toE164: string;
    templateName: string;
    templateLanguage: string;
    customerName: string;
    offerText: string;
    offerScope?: string;
    offerDate: string;
    urlButtonSuffix: string;
    bodyParamMode?: string;
    hasUrlButton?: boolean;
    headerParameters?: unknown[] | null;
  }): Promise<SendResult> {
    const mode = (input.bodyParamMode || 'name_offer_scope_date').trim().toLowerCase();
    const namedBody =
      mode === 'name_offer_scope_date' || mode === 'promo_offer'
        ? [
            { parameter_name: 'customer_name', text: input.customerName.trim() || 'Customer' },
            { parameter_name: 'discount', text: input.offerText.trim() },
            {
              parameter_name: 'product_offer',
              text: (input.offerScope || '').trim() || 'All Products',
            },
            { parameter_name: 'expiry_date', text: input.offerDate.trim() },
          ]
        : null;

    const bodyParams = namedBody ? namedBody.map((p) => p.text) : this.buildPromoBodyParams(mode, input);
    const urlParam = input.urlButtonSuffix.trim();
    // promo_offer on this WABA has a static URL button — Meta rejects button parameters.
    const wantsUrl = input.hasUrlButton === true && Boolean(urlParam);

    return await this.sendTemplate({
      phoneNumberId: input.phoneNumberId,
      accessToken: input.accessToken,
      toE164: input.toE164,
      templateName: input.templateName,
      templateLanguage: input.templateLanguage,
      bodyParams,
      ...(namedBody ? { namedBodyParams: namedBody } : {}),
      headerParameters: input.headerParameters ?? null,
      ...(wantsUrl ? { urlButtonParams: [{ index: '0', text: urlParam }] } : {}),
      omitEmptyBody: bodyParams.length === 0,
    });
  }

  /**
   * Create marketing template matching promo_offer: name, offer, scope, date + URL button.
   */
  async createPromoOfferTemplate(input: {
    wabaId: string;
    accessToken: string;
    templateName?: string;
    language?: string;
    urlBase?: string;
  }): Promise<{ id: string; status: string; name: string; language: string }> {
    const wabaId = input.wabaId.trim();
    if (!wabaId) throw new BadRequestException('businessAccountId (WABA id) is required to create a promo template');
    const name = (input.templateName || 'promo_offer').trim().toLowerCase().replace(/[^a-z0-9_]/g, '_');
    const language = (input.language || 'en').trim();
    const urlBase = (input.urlBase || 'https://yourdomain.com/').replace(/\{\{.*\}\}/, '').replace(/\/?$/, '/');
    const token = this.normalizeToken(input.accessToken);
    const url = `https://graph.facebook.com/${this.graphVersion()}/${wabaId}/message_templates`;
    const payload = {
      name,
      language,
      category: 'MARKETING',
      allow_category_change: true,
      components: [
        {
          type: 'BODY',
          text: 'Hello {{1}}, enjoy {{2}}% off on {{3}} — valid until {{4}}.',
          example: { body_text: [['John', '20', 'All Products', '31 July 2026']] },
        },
        {
          type: 'BUTTONS',
          buttons: [
            {
              type: 'URL',
              text: 'View offer',
              url: `${urlBase}{{1}}`,
              example: ['offers'],
            },
          ],
        },
      ],
    };
    const res = await fetch(url, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${token}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(payload),
    });
    const body = (await res.json()) as {
      id?: string;
      status?: string;
      error?: { message?: string; code?: number; error_data?: { details?: string } };
    };
    if (!res.ok) {
      throw new BadRequestException(
        this.formatMetaError(body.error, wabaId, 'create-template', name, language).replace(
          /phoneNumberId=/g,
          'wabaId=',
        ),
      );
    }
    if (!body.id) throw new BadRequestException('Meta template create returned no id');
    return { id: body.id, status: body.status || 'PENDING', name, language };
  }

  private buildPromoBodyParams(
    mode: string,
    input: { customerName: string; offerText: string; offerScope?: string; offerDate: string },
  ): string[] {
    if (mode === 'name_offer') {
      return [input.customerName.trim() || 'Customer', input.offerText.trim()];
    }
    if (mode === 'name_offer_date') {
      return [
        input.customerName.trim() || 'Customer',
        input.offerText.trim(),
        input.offerDate.trim(),
      ];
    }
    if (mode === 'name_offer_scope_date' || mode === 'promo_offer') {
      return [
        input.customerName.trim() || 'Customer',
        input.offerText.trim(),
        (input.offerScope || '').trim() || 'All Products',
        input.offerDate.trim(),
      ];
    }
    return [];
  }

  private parseExpectedBodyParamCount(message: string): number | null {
    const m = message.match(/expected number of params\s*\((\d+)\)/i);
    if (!m?.[1]) return null;
    return Number(m[1]);
  }

  private exceptionMessage(err: unknown): string {
    if (err instanceof BadRequestException) {
      const res = err.getResponse();
      if (typeof res === 'string') return res;
      if (res && typeof res === 'object' && 'message' in res) {
        const m = (res as { message?: string | string[] }).message;
        if (Array.isArray(m)) return m.join('; ');
        if (typeof m === 'string') return m;
      }
    }
    if (err instanceof Error) return err.message;
    return String(err);
  }

  async sendSessionTextMessage(input: {
    phoneNumberId: string;
    accessToken: string;
    toE164: string;
    text: string;
  }): Promise<SendResult> {
    const id = input.phoneNumberId.trim();
    const token = this.normalizeToken(input.accessToken);
    const url = `https://graph.facebook.com/${this.graphVersion()}/${id}/messages`;
    const res = await fetch(url, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${token}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        messaging_product: 'whatsapp',
        to: input.toE164,
        type: 'text',
        text: { preview_url: false, body: input.text },
      }),
    });
    const body = (await res.json()) as {
      messages?: Array<{ id?: string }>;
      error?: { message?: string; code?: number };
    };
    if (!res.ok) {
      const msg = body.error?.message ?? `WhatsApp session send failed (${res.status})`;
      this.logger.warn(`WhatsApp session send failed: ${msg}`);
      const err = new BadRequestException(msg) as BadRequestException & { metaCode?: number };
      const metaCode = body.error?.code;
      if (metaCode !== undefined) err.metaCode = metaCode;
      throw err;
    }
    const messageId = body.messages?.[0]?.id;
    if (!messageId) throw new BadRequestException('WhatsApp send returned no message id');
    return { messageId };
  }

  private async sendTemplate(input: {
    phoneNumberId: string;
    accessToken: string;
    toE164: string;
    templateName: string;
    templateLanguage: string;
    bodyParams: string[];
    /** When set, send named body parameters (Meta named-parameter templates). */
    namedBodyParams?: Array<{ parameter_name: string; text: string }>;
    headerParameters: unknown[] | null;
    urlButtonParams?: Array<{ index: string; text: string }>;
    /** When true and bodyParams empty, do not send a body component (static template). */
    omitEmptyBody?: boolean;
  }): Promise<SendResult> {
    const components: unknown[] = [];
    if (input.headerParameters?.length) {
      components.push({
        type: 'header',
        parameters: input.headerParameters,
      });
    }
    if (input.namedBodyParams?.length) {
      components.push({
        type: 'body',
        parameters: input.namedBodyParams.map((p) => ({
          type: 'text',
          parameter_name: p.parameter_name,
          text: p.text,
        })),
      });
    } else if (input.bodyParams.length > 0 || !input.omitEmptyBody) {
      if (input.bodyParams.length > 0) {
        components.push({
          type: 'body',
          parameters: input.bodyParams.map((text) => ({ type: 'text', text })),
        });
      }
    }
    for (const btn of input.urlButtonParams ?? []) {
      components.push({
        type: 'button',
        sub_type: 'url',
        index: btn.index,
        parameters: [{ type: 'text', text: btn.text }],
      });
    }
    const payload: Record<string, unknown> = {
      messaging_product: 'whatsapp',
      to: input.toE164,
      type: 'template',
      template: {
        name: input.templateName,
        language: { code: input.templateLanguage },
        ...(components.length ? { components } : {}),
      },
    };

    const id = input.phoneNumberId.trim();
    const token = this.normalizeToken(input.accessToken);
    const url = `https://graph.facebook.com/${this.graphVersion()}/${id}/messages`;
    const res = await fetch(url, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${token}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(payload),
    });
    const body = (await res.json()) as {
      messages?: Array<{ id?: string }>;
      error?: { message?: string; code?: number; error_data?: { details?: string } };
    };
    if (!res.ok) {
      const details = body.error?.error_data?.details;
      this.logger.warn(
        `WhatsApp send failed (phoneNumberId=${id}, template=${input.templateName}, lang=${input.templateLanguage}): ${body.error?.message}` +
          (details ? ` | ${details}` : ''),
      );
      throw new BadRequestException(
        this.formatMetaError(body.error, id, 'send', input.templateName, input.templateLanguage),
      );
    }
    const messageId = body.messages?.[0]?.id;
    if (!messageId) throw new BadRequestException('WhatsApp send returned no message id');
    return { messageId };
  }

  /** Best-effort display info for the sending WhatsApp business number. */
  async getPhoneDisplayInfo(
    phoneNumberId: string,
    accessToken: string,
  ): Promise<{ displayPhoneNumber: string; verifiedName: string }> {
    try {
      const id = phoneNumberId.trim();
      const token = this.normalizeToken(accessToken);
      const url = `https://graph.facebook.com/${this.graphVersion()}/${id}?fields=display_phone_number,verified_name`;
      const res = await fetch(url, { headers: { Authorization: `Bearer ${token}` } });
      const body = (await res.json()) as {
        display_phone_number?: string;
        verified_name?: string;
      };
      if (!res.ok) return { displayPhoneNumber: '', verifiedName: '' };
      return {
        displayPhoneNumber: body.display_phone_number?.trim() || '',
        verifiedName: body.verified_name?.trim() || '',
      };
    } catch {
      return { displayPhoneNumber: '', verifiedName: '' };
    }
  }

  private formatMetaError(
    error: { message?: string; code?: number; error_data?: { details?: string } } | undefined,
    phoneNumberId: string,
    op: string,
    templateName?: string,
    templateLanguage?: string,
  ): string {
    const msg = error?.message ?? `WhatsApp ${op} failed`;
    const details = error?.error_data?.details?.trim();
    const withDetails = details ? `${msg} — ${details}` : msg;

    if (error?.code === 138000 || /Calling not enabled|Calling API/i.test(withDetails)) {
      return (
        `${withDetails} ` +
        `Your promo template likely has a Call button. Cloud API cannot send Call-button templates unless Calling is enabled. ` +
        `Create a new MARKETING template without a Call button (body variables and/or URL button only), then set promoTemplateName to that template.`
      );
    }
    if (error?.code === 132000 || /number of parameters does not match/i.test(withDetails)) {
      return (
        `${withDetails} ` +
        `promoBodyParamMode / promoHasUrlButton must match the Meta template exactly ` +
        `(static body → promoBodyParamMode=none + promoHasUrlButton=false; ` +
        `3 vars + URL → name_offer_date + promoHasUrlButton=true). ` +
        `Do not copy invoice_send's language onto promo — use the promo template's own language.`
      );
    }
    if (error?.code === 132001 || /template name does not exist/i.test(withDetails)) {
      return (
        `${withDetails} ` +
        `promoTemplateName="${templateName}" promoTemplateLanguage="${templateLanguage}" must match Meta ` +
        `(invoice language en_US is separate — this store's promo_broadcast exists as "en", not "en_US").`
      );
    }
    if (/does not exist|missing permissions|Unsupported post request/i.test(msg)) {
      return (
        `${withDetails} ` +
        `(phoneNumberId=${phoneNumberId}, graph=${this.graphVersion()}). ` +
        `Use the WhatsApp Phone number ID from Meta → API Setup (not the WABA id or display number). ` +
        `Token must be a permanent System User token for that same WABA with whatsapp_business_messaging.`
      );
    }
    return withDetails;
  }
}
