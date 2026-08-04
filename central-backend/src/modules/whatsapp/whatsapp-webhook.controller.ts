import { Controller, Get, Post, Query, Body, Logger, Res, UsePipes, ValidationPipe } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import type { Response } from 'express';
import { WhatsAppBroadcastService } from './whatsapp-broadcast.service';

/**
 * Meta Cloud API webhooks (no JWT — Meta cannot send your app token).
 * Configure in Meta App → WhatsApp → Configuration:
 *   Callback URL: https://YOUR_PUBLIC_HOST/api/whatsapp/webhook
 *   Verify token: same as WHATSAPP_WEBHOOK_VERIFY_TOKEN in .env
 * Subscribe to: messages
 */
@ApiTags('whatsapp-webhook')
@Controller('whatsapp/webhook')
export class WhatsAppWebhookController {
  private readonly logger = new Logger(WhatsAppWebhookController.name);

  constructor(private readonly broadcastService: WhatsAppBroadcastService) {}

  @Get()
  verify(@Query() query: Record<string, string>, @Res() res: Response) {
    const mode = query['hub.mode'];
    const token = query['hub.verify_token'];
    const challenge = query['hub.challenge'];
    const expected = process.env.WHATSAPP_WEBHOOK_VERIFY_TOKEN?.trim() || 'rr-bridal-wa-verify';
    if (mode === 'subscribe' && token === expected && challenge) {
      this.logger.log('WhatsApp webhook verified');
      return res.status(200).type('text/plain').send(challenge);
    }
    this.logger.warn('WhatsApp webhook verification failed');
    return res.status(403).json({ error: 'Verification failed' });
  }

  @Post()
  @UsePipes(new ValidationPipe({ whitelist: false, forbidNonWhitelisted: false, transform: false }))
  async receive(@Body() body: unknown) {
    try {
      await this.broadcastService.applyMetaStatusWebhook(body);
    } catch (err) {
      this.logger.warn(`Webhook processing error: ${err instanceof Error ? err.message : String(err)}`);
    }
    // Always 200 so Meta does not retry forever on our bugs
    return { success: true };
  }
}
