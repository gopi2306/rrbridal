import { Body, Controller, Delete, Get, Param, Patch, Post, Query, Req, Res, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiOperation, ApiProduces, ApiTags } from '@nestjs/swagger';
import type { Request, Response } from 'express';
import { createReadStream } from 'fs';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { BillingClientPackageService } from './billing-client-package.service';
import { BillingClientDownloadQueryDto } from './dto/billing-client-download-query.dto';
import { CreateStoreDto } from './dto/create-store.dto';
import { FilterStoreDto } from './dto/filter-store.dto';
import { UpdateStoreDto } from './dto/update-store.dto';
import { WhatsAppSettingsDto } from './dto/whatsapp-settings.dto';
import { StoresService } from './stores.service';

function resolveRequestApiBase(req: Request): string {
  const xfProto = (req.headers['x-forwarded-proto'] as string | undefined)?.split(',')[0]?.trim();
  const xfHost = (req.headers['x-forwarded-host'] as string | undefined)?.split(',')[0]?.trim();
  const proto = xfProto || req.protocol || 'http';
  const host = xfHost || req.get('host') || '';
  if (!host) return '';
  return `${proto}://${host}`;
}

@ApiTags('admin-stores')
@ApiBearerAuth()
@Controller('admin/stores')
@UseGuards(JwtAuthGuard)
export class AdminStoresController {
  constructor(
    private readonly storesService: StoresService,
    private readonly billingClientPackageService: BillingClientPackageService,
  ) {}

  @Post()
  async create(@Body() dto: CreateStoreDto) {
    return await this.storesService.create(dto);
  }

  @Post('filter')
  async filter(@Body() dto: FilterStoreDto) {
    return await this.storesService.filter(dto);
  }

  @Get()
  async list() {
    return await this.storesService.findAll();
  }

  @Get(':code/billing-client')
  @ApiOperation({
    summary: 'Download store billing WPF client (format=exe|zip)',
    description:
      'format=zip (default): full self-contained folder + .env. format=exe: tiny zip with PublishSingleFile EXE + .env. ' +
      'Till CENTRAL_API_BASE defaults from PUBLIC_CENTRAL_API_BASE, else API_PUBLIC_ORIGIN, else this request host. ' +
      'zip needs BILLING_CLIENT_ARTIFACT_DIR; exe needs BILLING_CLIENT_SINGLE_EXE_PATH.',
  })
  @ApiProduces('application/zip')
  async downloadBillingClient(
    @Param('code') code: string,
    @Query() query: BillingClientDownloadQueryDto,
    @Req() req: Request,
    @Res() res: Response,
  ) {
    const format = query.format ?? 'zip';
    const result = await this.billingClientPackageService.buildPackage(
      code,
      query.posCounter,
      format,
      resolveRequestApiBase(req),
    );
    try {
      res.setHeader('Content-Type', 'application/zip');
      res.setHeader('Content-Disposition', `attachment; filename="${result.filename}"`);
      res.setHeader('Access-Control-Expose-Headers', 'Content-Disposition');
      await new Promise<void>((resolve, reject) => {
        const stream = createReadStream(result.zipPath);
        stream.on('error', reject);
        res.on('error', reject);
        res.on('finish', () => resolve());
        stream.pipe(res);
      });
    } finally {
      await result.cleanup();
    }
  }

  @Get(':code')
  async get(@Param('code') code: string) {
    return await this.storesService.findByCode(code);
  }

  @Patch(':code')
  async update(@Param('code') code: string, @Body() dto: UpdateStoreDto) {
    return await this.storesService.update(code, dto);
  }

  @Patch(':code/whatsapp-settings')
  async updateWhatsAppSettings(@Param('code') code: string, @Body() dto: WhatsAppSettingsDto) {
    return await this.storesService.updateWhatsAppSettings(code, dto);
  }

  @Delete(':code')
  async remove(@Param('code') code: string) {
    return await this.storesService.removeByCode(code);
  }
}
