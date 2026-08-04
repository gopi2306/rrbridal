import {
  BadRequestException,
  Injectable,
  Logger,
  NotFoundException,
  OnModuleDestroy,
  OnModuleInit,
} from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { StoresService } from '../stores/stores.service';
import { normalizeWhatsAppPhone } from './whatsapp.util';
import { WhatsAppCloudService } from './whatsapp-cloud.service';
import {
  WhatsAppBroadcastJob,
  WhatsAppBroadcastJobDocument,
} from './schemas/whatsapp-broadcast-job.schema';

const MAX_RECIPIENTS = 5000;
const SEND_DELAY_MS = 250;

export type BroadcastRecipientInput = { name?: string; phone: string };

@Injectable()
export class WhatsAppBroadcastService implements OnModuleInit, OnModuleDestroy {
  private readonly logger = new Logger(WhatsAppBroadcastService.name);
  private processing = false;
  private timer: ReturnType<typeof setInterval> | null = null;

  constructor(
    private readonly storesService: StoresService,
    private readonly cloud: WhatsAppCloudService,
    @InjectModel(WhatsAppBroadcastJob.name)
    private readonly jobModel: Model<WhatsAppBroadcastJobDocument>,
  ) {}

  onModuleInit() {
    this.timer = setInterval(() => {
      void this.processQueue();
    }, 4000);
  }

  onModuleDestroy() {
    if (this.timer) clearInterval(this.timer);
  }

  async startBroadcast(input: {
    storeId: string;
    mode?: string;
    promoText: string;
    offerScope?: string;
    offerDate?: string;
    urlButtonSuffix?: string;
    recipients: BroadcastRecipientInput[];
    attachment?: Buffer;
    attachmentFilename?: string;
    attachmentMimeType?: string;
  }): Promise<{ jobId: string }> {
    const code = input.storeId.trim().toLowerCase();
    if (!code) throw new BadRequestException('storeId is required');

    const promoText = (input.promoText ?? '').trim();
    const mode = (input.mode ?? 'template').trim().toLowerCase() === 'session' ? 'session' : 'template';
    const offerScope = (input.offerScope ?? '').trim();
    const offerDate = (input.offerDate ?? '').trim();
    const urlButtonSuffix = (input.urlButtonSuffix ?? '').trim();

    if (mode === 'session' && !promoText) {
      throw new BadRequestException('promoText is required for session mode');
    }
    const store = await this.storesService.findByCode(code);
    const creds = this.storesService.resolveWhatsAppCredentials(
      store.whatsappSettings as Record<string, unknown> | undefined,
    );

    if (!creds.enabled) throw new BadRequestException('WhatsApp is disabled for this store');
    if (!creds.phoneNumberId || !creds.accessToken) {
      throw new BadRequestException('WhatsApp is not fully configured for this store');
    }
    if (mode === 'template') {
      const promoName = (creds.promoTemplateName || 'promo_offer').trim();
      if (!promoName) throw new BadRequestException('promoTemplateName is not configured');
      const bodyMode = (creds.promoBodyParamMode || 'name_offer_scope_date').toLowerCase();
      if (promoName === 'promo_broadcast') {
        throw new BadRequestException(
          'promo_broadcast is the old Call-button template. Set promoTemplateName to "promo_offer" ' +
            '(4 body vars: name, offer, scope, date + URL) to match your Meta curl.',
        );
      }
      if ((bodyMode === 'name_offer_date' || bodyMode === 'name_offer_scope_date') && !offerDate) {
        throw new BadRequestException('offerDate is required for this promo template');
      }
      if (bodyMode === 'name_offer_scope_date' && !offerScope) {
        throw new BadRequestException('offerScope is required (template {{3}}, e.g. All Products)');
      }
      if (bodyMode !== 'none' && !promoText) {
        throw new BadRequestException('promoText (offer) is required');
      }
      if (creds.promoHasUrlButton && !urlButtonSuffix) {
        throw new BadRequestException('urlButtonSuffix is required (full URL or path matching Meta button)');
      }
    }

    if (!input.recipients?.length) throw new BadRequestException('At least one recipient is required');
    if (input.recipients.length > MAX_RECIPIENTS) {
      throw new BadRequestException(`Maximum ${MAX_RECIPIENTS} recipients per broadcast job`);
    }

    const headerType = (creds.promoHeaderType || 'none').toLowerCase();
    let mediaId: string | undefined;
    let mediaFilename: string | undefined;
    let mediaMimeType: string | undefined;

    if (mode === 'template' && (headerType === 'image' || headerType === 'document')) {
      if (!input.attachment?.length) {
        throw new BadRequestException(`Attachment required when promoHeaderType is ${headerType}`);
      }
      mediaFilename = input.attachmentFilename?.trim() || (headerType === 'document' ? 'promo.pdf' : 'promo.png');
      mediaMimeType = input.attachmentMimeType;
      const uploaded =
        headerType === 'document'
          ? await this.cloud.uploadDocument(
              creds.phoneNumberId,
              creds.accessToken,
              input.attachment,
              mediaFilename,
            )
          : await this.cloud.uploadImage(
              creds.phoneNumberId,
              creds.accessToken,
              input.attachment,
              mediaFilename,
            );
      mediaId = uploaded.id;
    }

    const recipients = input.recipients.map((r) => {
      const phone = (r.phone ?? '').trim();
      const phoneE164 = normalizeWhatsAppPhone(phone, creds.defaultCountryCode);
      const valid = Boolean(phoneE164 && phoneE164.length >= 11);
      return {
        name: (r.name ?? '').trim() || 'Customer',
        phone,
        phoneE164: valid ? phoneE164 : undefined,
        status: valid ? 'pending' : 'skipped',
        ...(valid ? {} : { error: 'Invalid phone' }),
      };
    });

    const skipped = recipients.filter((r) => r.status === 'skipped').length;
    const job = await this.jobModel.create({
      storeId: code,
      mode,
      promoText: promoText || ' ',
      offerScope,
      offerDate,
      urlButtonSuffix,
      status: 'queued',
      recipients,
      total: recipients.length,
      sent: 0,
      failed: 0,
      skipped,
      ...(mediaId ? { mediaId } : {}),
      ...(mediaFilename ? { mediaFilename } : {}),
      ...(mediaMimeType ? { mediaMimeType } : {}),
    });

    void this.processQueue();
    return { jobId: String(job._id) };
  }

  async getJob(jobId: string) {
    const job = await this.jobModel.findById(jobId).lean();
    if (!job) throw new NotFoundException('Broadcast job not found');

    let fromDisplayPhone = '';
    let fromVerifiedName = '';
    try {
      const store = await this.storesService.findByCode(job.storeId);
      const creds = this.storesService.resolveWhatsAppCredentials(
        store.whatsappSettings as Record<string, unknown> | undefined,
      );
      if (creds.phoneNumberId && creds.accessToken) {
        const info = await this.cloud.getPhoneDisplayInfo(creds.phoneNumberId, creds.accessToken);
        fromDisplayPhone = info.displayPhoneNumber;
        fromVerifiedName = info.verifiedName;
      }
    } catch {
      // best-effort only
    }

    return {
      jobId: String(job._id),
      storeId: job.storeId,
      mode: job.mode,
      promoText: job.promoText,
      offerScope: job.offerScope,
      offerDate: job.offerDate,
      urlButtonSuffix: job.urlButtonSuffix,
      status: job.status,
      total: job.total,
      sent: job.sent,
      failed: job.failed,
      skipped: job.skipped,
      error: job.error,
      startedAt: job.startedAt,
      finishedAt: job.finishedAt,
      ...(fromVerifiedName ? { fromVerifiedName } : {}),
      ...(fromDisplayPhone ? { fromDisplayPhone } : {}),
      deliveryNote:
        'status=sent / metaAccepted means Meta Cloud API accepted the request (wamid). ' +
        'That is NOT phone delivery. deliveryStatus comes from Meta webhooks (sent→delivered→read or failed). ' +
        `Look on WhatsApp for ${fromVerifiedName || 'business'} ${fromDisplayPhone || ''}. ` +
        'Marketing templates may be accepted then dropped by Meta (e.g. engagement limits) — check deliveryStatus/deliveryError. ' +
        'Configure POST https://YOUR_PUBLIC_HOST/api/whatsapp/webhook in Meta App → WhatsApp → Configuration.',
      results: (job.recipients ?? []).map((r) => ({
        name: r.name,
        phone: r.phone,
        phoneE164: r.phoneE164,
        status: r.status,
        messageId: r.messageId,
        error: r.error,
        metaAccepted: r.status === 'sent' && Boolean(r.messageId),
        deliveryStatus: r.deliveryStatus,
        deliveryError: r.deliveryError,
        deliveryAt: r.deliveryAt,
      })),
    };
  }

  /**
   * Apply Meta "statuses" webhook updates onto broadcast recipients by messageId (wamid).
   */
  async applyMetaStatusWebhook(body: unknown): Promise<void> {
    const root = body as {
      entry?: Array<{
        changes?: Array<{
          value?: {
            statuses?: Array<{
              id?: string;
              status?: string;
              timestamp?: string;
              errors?: Array<{ code?: number; title?: string; message?: string }>;
            }>;
          };
        }>;
      }>;
    };

    const statuses =
      root.entry?.flatMap((e) => e.changes ?? []).flatMap((c) => c.value?.statuses ?? []) ?? [];

    for (const st of statuses) {
      const messageId = st.id?.trim();
      const deliveryStatus = st.status?.trim().toLowerCase();
      if (!messageId || !deliveryStatus) continue;

      const errText = st.errors?.length
        ? st.errors
            .map((x) => [x.code, x.title || x.message].filter(Boolean).join(' '))
            .join('; ')
        : undefined;

      const job = await this.jobModel.findOne({ 'recipients.messageId': messageId });
      if (!job) {
        this.logger.debug(`Webhook status for unknown messageId=${messageId} (${deliveryStatus})`);
        continue;
      }

      const idx = job.recipients.findIndex((r) => r.messageId === messageId);
      if (idx < 0) continue;
      const row = job.recipients[idx];
      if (!row) continue;

      row.deliveryStatus = deliveryStatus;
      if (errText) row.deliveryError = errText;
      if (st.timestamp) {
        const ms = Number(st.timestamp) * 1000;
        if (!Number.isNaN(ms)) row.deliveryAt = new Date(ms);
      } else {
        row.deliveryAt = new Date();
      }

      // Surface hard delivery failures on the recipient row
      if (deliveryStatus === 'failed') {
        row.status = 'failed';
        row.error = errText || row.error || 'Meta delivery failed (webhook)';
        job.failed = (job.recipients.filter((r) => r.status === 'failed').length);
        job.sent = (job.recipients.filter((r) => r.status === 'sent').length);
      }

      job.markModified('recipients');
      await job.save();
      this.logger.log(
        `Broadcast ${String(job._id)} recipient ${messageId} → deliveryStatus=${deliveryStatus}` +
          (errText ? ` error=${errText}` : ''),
      );
    }
  }

  /**
   * Create Meta marketing template promo_offer (3 body vars + URL button) and point store settings at it.
   * Requires a valid WhatsApp Business Account ID in businessAccountId (Meta → API Setup → WABA ID).
   */
  async ensurePromoTemplate(input: {
    storeId: string;
    wabaId?: string;
    templateName?: string;
    language?: string;
    urlBase?: string;
  }) {
    const code = input.storeId.trim().toLowerCase();
    if (!code) throw new BadRequestException('storeId is required');
    const store = await this.storesService.findByCode(code);
    const creds = this.storesService.resolveWhatsAppCredentials(
      store.whatsappSettings as Record<string, unknown> | undefined,
    );
    if (!creds.accessToken) throw new BadRequestException('WhatsApp accessToken is not configured');

    const wabaId = (input.wabaId || creds.businessAccountId || '').trim();
    if (!wabaId) {
      throw new BadRequestException(
        'businessAccountId (WhatsApp Business Account ID / WABA) is required. ' +
          'Copy it from Meta Developer → WhatsApp → API Setup (not the Phone number ID), ' +
          'PATCH store whatsapp-settings.businessAccountId, then retry.',
      );
    }

    const created = await this.cloud.createPromoOfferTemplate({
      wabaId,
      accessToken: creds.accessToken,
      ...(input.templateName ? { templateName: input.templateName } : {}),
      ...(input.language ? { language: input.language } : {}),
      ...(input.urlBase ? { urlBase: input.urlBase } : {}),
    });

    await this.storesService.updateWhatsAppSettings(code, {
      businessAccountId: wabaId,
      promoTemplateName: created.name,
      promoTemplateLanguage: created.language,
      promoBodyParamMode: 'name_offer_scope_date',
      promoHasUrlButton: false,
      promoHeaderType: 'none',
      enabled: true,
    });

    return {
      ...created,
      message:
        created.status === 'APPROVED'
          ? 'Template approved — broadcast can use it now.'
          : `Template created with status ${created.status}. Wait until Meta shows APPROVED, then broadcast.`,
    };
  }

  async processQueue(): Promise<void> {
    if (this.processing) return;
    this.processing = true;
    try {
      // eslint-disable-next-line no-constant-condition
      while (true) {
        const job = await this.jobModel.findOneAndUpdate(
          { status: 'queued' },
          { $set: { status: 'running', startedAt: new Date() } },
          { new: true, sort: { createdAt: 1 } },
        );
        if (!job) break;
        await this.runJob(job);
      }
    } catch (err) {
      this.logger.warn(`Broadcast queue error: ${err instanceof Error ? err.message : String(err)}`);
    } finally {
      this.processing = false;
    }
  }

  private async runJob(job: WhatsAppBroadcastJobDocument): Promise<void> {
    try {
      const store = await this.storesService.findByCode(job.storeId);
      const creds = this.storesService.resolveWhatsAppCredentials(
        store.whatsappSettings as Record<string, unknown> | undefined,
      );
      const promoName = creds.promoTemplateName || 'promo_offer';
      const promoLang = creds.promoTemplateLanguage || 'en';
      const headerType = (creds.promoHeaderType || 'none').toLowerCase();

      for (let i = 0; i < job.recipients.length; i++) {
        const r = job.recipients[i];
        if (!r || r.status !== 'pending') continue;

        try {
          if (!r.phoneE164) {
            r.status = 'skipped';
            r.error = 'Invalid phone';
            job.skipped += 1;
          } else if (job.mode === 'session') {
            const text = this.buildSessionText(r.name, job.promoText, job.offerDate);
            const sent = await this.cloud.sendSessionTextMessage({
              phoneNumberId: creds.phoneNumberId,
              accessToken: creds.accessToken,
              toE164: r.phoneE164,
              text,
            });
            r.status = 'sent';
            r.messageId = sent.messageId;
            job.sent += 1;
          } else {
            let headerParameters: unknown[] | null = null;
            if (headerType === 'image' && job.mediaId) {
              headerParameters = [{ type: 'image', image: { id: job.mediaId } }];
            } else if (headerType === 'document' && job.mediaId) {
              const document: { id: string; filename?: string } = { id: job.mediaId };
              if (job.mediaFilename) document.filename = job.mediaFilename;
              headerParameters = [{ type: 'document', document }];
            }

            const sent = await this.cloud.sendPromoBroadcastTemplate({
              phoneNumberId: creds.phoneNumberId,
              accessToken: creds.accessToken,
              toE164: r.phoneE164,
              templateName: promoName,
              templateLanguage: promoLang,
              customerName: r.name || 'Customer',
              offerText: job.promoText,
              offerScope: job.offerScope || '',
              offerDate: job.offerDate || '',
              urlButtonSuffix: job.urlButtonSuffix || '',
              bodyParamMode: creds.promoBodyParamMode,
              hasUrlButton: creds.promoHasUrlButton,
              headerParameters,
            });
            r.status = 'sent';
            r.messageId = sent.messageId;
            job.sent += 1;
          }
        } catch (err) {
          const msg = exceptionMessage(err);
          if (job.mode === 'session' && this.isOutsideWindowError(msg)) {
            r.status = 'skipped';
            r.error = msg;
            job.skipped += 1;
          } else {
            r.status = 'failed';
            r.error = msg;
            job.failed += 1;
          }
        }

        job.markModified('recipients');
        await job.save();
        await sleep(SEND_DELAY_MS);
      }

      job.status = 'completed';
      job.finishedAt = new Date();
      await job.save();
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      this.logger.warn(`Broadcast job ${String(job._id)} failed: ${msg}`);
      job.status = 'failed';
      job.error = msg;
      job.finishedAt = new Date();
      await job.save();
    }
  }

  private buildSessionText(name: string, offer: string, date: string): string {
    const n = name.trim() || 'Customer';
    const parts = [`Hello ${n}`, offer.trim()];
    if (date.trim()) parts.push(`valid until ${date.trim()}`);
    return parts.join(', ').slice(0, 4096);
  }

  private isOutsideWindowError(msg: string): boolean {
    return /outside|24.?hour|re-?engage|not in allowed|131047|131026/i.test(msg);
  }
}

function exceptionMessage(err: unknown): string {
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

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
