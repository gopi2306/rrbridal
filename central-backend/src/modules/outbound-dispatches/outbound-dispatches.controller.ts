import {
  BadRequestException,
  Body,
  Controller,
  Get,
  Param,
  Post,
  Put,
  Query,
} from '@nestjs/common';
import { ApiQuery, ApiTags } from '@nestjs/swagger';
import { createHash, randomUUID } from 'crypto';
import { SyncEventDto } from '../sync/dto/sync-push.dto';
import { SyncService } from '../sync/sync.service';
import { OutboundDispatchesService } from './outbound-dispatches.service';

type DispatchWriteBody = {
  storeId?: string;
  storeCode?: string;
  deviceId: string;
  payload?: Record<string, unknown>;
  eventId?: string;
  hash?: string;
  createdAt?: string;
};

@ApiTags('store-pos outbound dispatches')
@Controller('store-pos/outbound-dispatches')
export class OutboundDispatchesController {
  constructor(
    private readonly syncService: SyncService,
    private readonly dispatches: OutboundDispatchesService,
  ) {}

  @Get()
  @ApiQuery({ name: 'storeCode', required: false })
  @ApiQuery({ name: 'businessDate', required: false })
  @ApiQuery({ name: 'status', required: false })
  @ApiQuery({ name: 'batchNo', required: false })
  @ApiQuery({ name: 'limit', required: false })
  async list(
    @Query('storeCode') storeCode?: string,
    @Query('businessDate') businessDate?: string,
    @Query('status') status?: string,
    @Query('batchNo') batchNo?: string,
    @Query('limit') limit?: string,
  ) {
    const parsed = limit ? Number(limit) : 100;
    return this.dispatches.list({
      ...(storeCode ? { storeCode } : {}),
      ...(businessDate ? { businessDate } : {}),
      ...(status ? { status } : {}),
      ...(batchNo ? { batchNo } : {}),
      limit: Number.isFinite(parsed) ? parsed : 100,
    });
  }

  @Get('by-bill/:billNo/active')
  @ApiQuery({ name: 'storeCode', required: true })
  async getActiveByBill(
    @Param('billNo') billNo: string,
    @Query('storeCode') storeCode: string,
  ) {
    return this.dispatches.getActiveByBill(storeCode, billNo);
  }

  @Get(':dispatchNo')
  @ApiQuery({ name: 'storeCode', required: true })
  async get(
    @Param('dispatchNo') dispatchNo: string,
    @Query('storeCode') storeCode: string,
  ) {
    return this.dispatches.get(storeCode, dispatchNo);
  }

  @Post()
  async create(@Body() body: DispatchWriteBody) {
    return this.apply('OutboundDispatchCreated', body);
  }

  @Put(':dispatchNo')
  async update(
    @Param('dispatchNo') dispatchNo: string,
    @Body() body: DispatchWriteBody,
  ) {
    return this.apply('OutboundDispatchUpdated', body, dispatchNo);
  }

  @Post(':dispatchNo/status')
  async changeStatus(
    @Param('dispatchNo') dispatchNo: string,
    @Body() body: DispatchWriteBody,
  ) {
    return this.apply('OutboundDispatchStatusChanged', body, dispatchNo);
  }

  @Post(':dispatchNo/charge')
  async receiveCharge(
    @Param('dispatchNo') dispatchNo: string,
    @Body() body: DispatchWriteBody,
  ) {
    return this.apply('OutboundDispatchChargeReceived', body, dispatchNo);
  }

  @Post(':dispatchNo/cancel')
  async cancel(
    @Param('dispatchNo') dispatchNo: string,
    @Body() body: DispatchWriteBody,
  ) {
    return this.apply('OutboundDispatchCancelled', body, dispatchNo);
  }

  private async apply(type: string, body: DispatchWriteBody, dispatchNo?: string) {
    if (!body || typeof body !== 'object') {
      throw new BadRequestException('Request body is required');
    }
    const storeId = (body.storeId ?? body.storeCode)?.trim();
    const deviceId = body.deviceId?.trim();
    if (!storeId || !deviceId) {
      throw new BadRequestException('storeId/storeCode and deviceId are required');
    }
    const payload = {
      ...(body.payload ?? {}),
      ...(dispatchNo ? { dispatchNo } : {}),
    };
    const event: SyncEventDto = {
      eventId: body.eventId?.trim() || randomUUID(),
      storeId,
      deviceId,
      type,
      createdAt: body.createdAt?.trim() || new Date().toISOString(),
      payload,
      hash:
        body.hash?.trim() ||
        createHash('sha256').update(JSON.stringify(payload)).digest('hex'),
    };
    const result = await this.syncService.applyOne(event);
    if (result.status === 'rejected') {
      throw new BadRequestException(result.reason || `Event ${type} was rejected`);
    }
    return { ...result, eventId: event.eventId };
  }
}
