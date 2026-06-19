import {
  BadRequestException,
  Body,
  Controller,
  Get,
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
import { SendWhatsAppInvoiceFieldsDto, WhatsAppSettingsQueryDto, WhatsAppTestSendDto } from './dto/whatsapp.dto';
import { WhatsAppInvoiceService } from './whatsapp-invoice.service';

@ApiTags('whatsapp')
@ApiBearerAuth()
@Controller('whatsapp')
@UseGuards(JwtAuthGuard)
export class WhatsAppController {
  constructor(private readonly invoiceService: WhatsAppInvoiceService) {}

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
    if (!attachment?.buffer?.length) {
      throw new BadRequestException('attachment file is required');
    }
    return await this.invoiceService.sendInvoice({
      storeId: body.storeId ?? '',
      billNo: 'TEST',
      customerName: body.customerName ?? 'Test Customer',
      customerPhone: body.customerPhone,
      payable: 0,
      attachment: attachment.buffer,
      attachmentFilename: attachment.originalname || 'test-bill.png',
    });
  }
}
