import {
  BadRequestException,
  Body,
  Controller,
  Get,
  Param,
  Post,
  Query,
  UploadedFile,
  UseGuards,
  UseInterceptors,
} from '@nestjs/common';
import { FileInterceptor } from '@nestjs/platform-express';
import { ApiBearerAuth, ApiConsumes, ApiTags } from '@nestjs/swagger';
import { memoryStorage } from 'multer';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { WhatsAppBroadcastFieldsDto, WhatsAppBroadcastJsonDto } from './dto/whatsapp-broadcast.dto';
import { EnsurePromoTemplateDto } from './dto/ensure-promo-template.dto';
import { SendWhatsAppInvoiceFieldsDto, WhatsAppSettingsQueryDto, WhatsAppTestSendDto } from './dto/whatsapp.dto';
import { WhatsAppBroadcastService } from './whatsapp-broadcast.service';
import { WhatsAppInvoiceService } from './whatsapp-invoice.service';

@ApiTags('whatsapp')
@ApiBearerAuth()
@Controller('whatsapp')
@UseGuards(JwtAuthGuard)
export class WhatsAppController {
  constructor(
    private readonly invoiceService: WhatsAppInvoiceService,
    private readonly broadcastService: WhatsAppBroadcastService,
  ) {}

  @Get('settings')
  async getSettings(@Query() query: WhatsAppSettingsQueryDto) {
    return await this.invoiceService.getSettings(query.storeId);
  }

  @Post('send-invoice')
  @ApiConsumes('multipart/form-data')
  @UseInterceptors(
    FileInterceptor('attachment', {
      storage: memoryStorage(),
      limits: { fileSize: 8 * 1024 * 1024 },
    }),
  )
  async sendInvoice(
    @Body() body: SendWhatsAppInvoiceFieldsDto,
    @UploadedFile() attachment: Express.Multer.File | undefined,
  ) {
    return await this.invoiceService.sendInvoice({
      storeId: body.storeId,
      billNo: body.billNo,
      customerName: body.customerName ?? '',
      customerPhone: body.customerPhone,
      payable: body.payable,
      attachment: attachment?.buffer ?? Buffer.alloc(0),
      ...(attachment?.originalname ? { attachmentFilename: attachment.originalname } : {}),
      ...(attachment?.mimetype ? { attachmentMimeType: attachment.mimetype } : {}),
    });
  }

  @Post('test')
  @ApiConsumes('multipart/form-data')
  @UseInterceptors(
    FileInterceptor('attachment', {
      storage: memoryStorage(),
      limits: { fileSize: 8 * 1024 * 1024 },
    }),
  )
  async testSend(
    @Body() body: WhatsAppTestSendDto,
    @UploadedFile() attachment: Express.Multer.File | undefined,
  ) {
    return await this.invoiceService.sendTest({
      storeId: body.storeId ?? '',
      customerName: body.customerName ?? 'Test Customer',
      customerPhone: body.customerPhone,
      ...(attachment?.buffer?.length
        ? {
            attachment: attachment.buffer,
            attachmentFilename: attachment.originalname || 'test-bill.png',
            ...(attachment.mimetype ? { attachmentMimeType: attachment.mimetype } : {}),
          }
        : {}),
    });
  }

  @Post('broadcast')
  @ApiConsumes('multipart/form-data', 'application/json')
  @UseInterceptors(
    FileInterceptor('attachment', {
      storage: memoryStorage(),
      limits: { fileSize: 8 * 1024 * 1024 },
    }),
  )
  async startBroadcast(
    @Body() body: WhatsAppBroadcastFieldsDto,
    @UploadedFile() attachment: Express.Multer.File | undefined,
  ) {
    const recipients = parseRecipients(body);
    return await this.broadcastService.startBroadcast({
      storeId: body.storeId,
      mode: body.mode ?? 'template',
      promoText: body.promoText,
      recipients,
      ...(body.offerScope ? { offerScope: body.offerScope } : {}),
      ...(body.offerDate ? { offerDate: body.offerDate } : {}),
      ...(body.urlButtonSuffix ? { urlButtonSuffix: body.urlButtonSuffix } : {}),
      ...(attachment?.buffer?.length
        ? {
            attachment: attachment.buffer,
            attachmentFilename: attachment.originalname || 'promo.bin',
            ...(attachment.mimetype ? { attachmentMimeType: attachment.mimetype } : {}),
          }
        : {}),
    });
  }

  /** JSON body only (no file upload). Same job queue as POST /broadcast. */
  @Post('broadcast/json')
  @ApiConsumes('application/json')
  async startBroadcastJson(@Body() body: WhatsAppBroadcastJsonDto) {
    const recipients = body.recipients.map((r) => ({
      phone: r.phone,
      ...(r.name ? { name: r.name } : {}),
    }));
    return await this.broadcastService.startBroadcast({
      storeId: body.storeId,
      mode: body.mode ?? 'template',
      promoText: body.promoText,
      recipients,
      ...(body.offerScope ? { offerScope: body.offerScope } : {}),
      ...(body.offerDate ? { offerDate: body.offerDate } : {}),
      ...(body.urlButtonSuffix ? { urlButtonSuffix: body.urlButtonSuffix } : {}),
    });
  }

  @Get('broadcast/:jobId')
  async getBroadcast(@Param('jobId') jobId: string) {
    return await this.broadcastService.getJob(jobId);
  }

  @Post('ensure-promo-template')
  async ensurePromoTemplate(@Body() body: EnsurePromoTemplateDto) {
    return await this.broadcastService.ensurePromoTemplate({
      storeId: body.storeId,
      ...(body.wabaId ? { wabaId: body.wabaId } : {}),
      ...(body.templateName ? { templateName: body.templateName } : {}),
      ...(body.language ? { language: body.language } : {}),
      ...(body.urlBase ? { urlBase: body.urlBase } : {}),
    });
  }
}

function parseRecipients(body: WhatsAppBroadcastFieldsDto): Array<{ name?: string; phone: string }> {
  if (body.recipients?.length) {
    return body.recipients.map((r) => ({
      phone: r.phone,
      ...(r.name ? { name: r.name } : {}),
    }));
  }
  const raw = body.recipientsJson?.trim();
  if (!raw) throw new BadRequestException('recipients or recipientsJson is required');
  try {
    const parsed = JSON.parse(raw) as unknown;
    if (!Array.isArray(parsed) || !parsed.length) {
      throw new BadRequestException('recipientsJson must be a non-empty array');
    }
    return parsed.map((item) => {
      const row = item as { name?: string; phone?: string };
      if (!row.phone?.trim()) throw new BadRequestException('Each recipient needs a phone');
      return {
        phone: row.phone,
        ...(row.name ? { name: row.name } : {}),
      };
    });
  } catch (err) {
    if (err instanceof BadRequestException) throw err;
    throw new BadRequestException('recipientsJson must be valid JSON');
  }
}
