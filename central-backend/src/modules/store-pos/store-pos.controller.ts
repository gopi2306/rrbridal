import {
  BadRequestException,
  Body,
  Controller,
  Delete,
  Get,
  Param,
  Post,
  Put,
  Query,
} from '@nestjs/common';
import { ApiQuery, ApiTags } from '@nestjs/swagger';
import { StorePosQueryService } from './store-pos-query.service';
import { StorePosService } from './store-pos.service';

type StorePosWriteBody = {
  storeId: string;
  deviceId: string;
  payload?: Record<string, unknown>;
  eventId?: string;
  hash?: string;
  createdAt?: string;
};

type StorePosEventBody = StorePosWriteBody & {
  type: string;
};

function requireWriteBody<T extends StorePosWriteBody>(body: T | undefined): T {
  if (!body || typeof body !== 'object') {
    throw new BadRequestException('Request body is required');
  }
  if (!body.storeId?.trim() || !body.deviceId?.trim()) {
    throw new BadRequestException('storeId and deviceId are required');
  }
  return body;
}

@ApiTags('store-pos')
@Controller('store-pos')
export class StorePosController {
  constructor(
    private readonly storePos: StorePosService,
    private readonly storePosQuery: StorePosQueryService,
  ) {}

  @Get('catalog/search')
  @ApiQuery({ name: 'storeCode', required: true })
  @ApiQuery({ name: 'q', required: true })
  @ApiQuery({ name: 'limit', required: false })
  async searchCatalog(
    @Query('storeCode') storeCode: string,
    @Query('q') q: string,
    @Query('limit') limit?: string,
  ) {
    const parsed = limit ? Number(limit) : 80;
    return await this.storePos.searchCatalog(storeCode, q, Number.isFinite(parsed) ? parsed : 80);
  }

  @Get('bills')
  @ApiQuery({ name: 'storeCode', required: true })
  @ApiQuery({ name: 'search', required: false })
  @ApiQuery({ name: 'limit', required: false })
  @ApiQuery({ name: 'creditBillingOnly', required: false })
  async listBills(
    @Query('storeCode') storeCode: string,
    @Query('search') search?: string,
    @Query('limit') limit?: string,
    @Query('creditBillingOnly') creditBillingOnly?: string,
  ) {
    const parsed = limit ? Number(limit) : 50;
    const creditOnly =
      creditBillingOnly === '1' ||
      creditBillingOnly === 'true' ||
      creditBillingOnly === 'yes';
    return await this.storePosQuery.listBills(
      storeCode,
      search,
      Number.isFinite(parsed) ? parsed : 50,
      creditOnly,
    );
  }

  @Get('bills/:billNo')
  @ApiQuery({ name: 'storeCode', required: true })
  async getBill(@Param('billNo') billNo: string, @Query('storeCode') storeCode: string) {
    return await this.storePosQuery.getBill(storeCode, billNo);
  }

  @Get('sale-returns')
  @ApiQuery({ name: 'storeCode', required: true })
  @ApiQuery({ name: 'originalBillNo', required: false })
  @ApiQuery({ name: 'limit', required: false })
  async listReturns(
    @Query('storeCode') storeCode: string,
    @Query('originalBillNo') originalBillNo?: string,
    @Query('limit') limit?: string,
  ) {
    const parsed = limit ? Number(limit) : 50;
    return await this.storePosQuery.listReturns(
      storeCode,
      originalBillNo,
      Number.isFinite(parsed) ? parsed : 50,
    );
  }

  @Get('sale-returns/:returnNo')
  @ApiQuery({ name: 'storeCode', required: true })
  async getReturn(@Param('returnNo') returnNo: string, @Query('storeCode') storeCode: string) {
    return await this.storePosQuery.getReturn(storeCode, returnNo);
  }

  @Get('quotations')
  @ApiQuery({ name: 'storeCode', required: true })
  @ApiQuery({ name: 'status', required: false })
  @ApiQuery({ name: 'limit', required: false })
  async listQuotations(
    @Query('storeCode') storeCode: string,
    @Query('status') status?: string,
    @Query('limit') limit?: string,
  ) {
    const parsed = limit ? Number(limit) : 50;
    return await this.storePosQuery.listQuotations(
      storeCode,
      status,
      Number.isFinite(parsed) ? parsed : 50,
    );
  }

  @Get('quotations/:quotationNo')
  @ApiQuery({ name: 'storeCode', required: true })
  async getQuotation(
    @Param('quotationNo') quotationNo: string,
    @Query('storeCode') storeCode: string,
  ) {
    return await this.storePosQuery.getQuotation(storeCode, quotationNo);
  }

  @Get('credit-notes')
  @ApiQuery({ name: 'storeCode', required: true })
  @ApiQuery({ name: 'customerPhone', required: false })
  @ApiQuery({ name: 'customerCode', required: false })
  @ApiQuery({ name: 'availableOnly', required: false })
  async listCreditNotes(
    @Query('storeCode') storeCode: string,
    @Query('customerPhone') customerPhone?: string,
    @Query('customerCode') customerCode?: string,
    @Query('availableOnly') availableOnly?: string,
  ) {
    return await this.storePosQuery.listCreditNotes(
      storeCode,
      customerPhone,
      customerCode,
      availableOnly === 'true' || availableOnly === '1',
    );
  }

  @Get('credit-notes/:creditNoteNo')
  @ApiQuery({ name: 'storeCode', required: true })
  async getCreditNote(
    @Param('creditNoteNo') creditNoteNo: string,
    @Query('storeCode') storeCode: string,
  ) {
    return await this.storePosQuery.getCreditNote(storeCode, creditNoteNo);
  }

  @Get('day-sessions/current')
  @ApiQuery({ name: 'storeCode', required: true })
  @ApiQuery({ name: 'businessDate', required: true })
  @ApiQuery({ name: 'posCounter', required: true })
  async getDaySession(
    @Query('storeCode') storeCode: string,
    @Query('businessDate') businessDate: string,
    @Query('posCounter') posCounter: string,
  ) {
    return await this.storePosQuery.getDaySession(storeCode, businessDate, posCounter);
  }

  @Get('cash-movements')
  @ApiQuery({ name: 'storeCode', required: true })
  @ApiQuery({ name: 'businessDate', required: true })
  @ApiQuery({ name: 'limit', required: false })
  async listCashMovements(
    @Query('storeCode') storeCode: string,
    @Query('businessDate') businessDate: string,
    @Query('limit') limit?: string,
  ) {
    const parsed = limit ? Number(limit) : 100;
    return await this.storePosQuery.listCashMovements(
      storeCode,
      businessDate,
      Number.isFinite(parsed) ? parsed : 100,
    );
  }

  @Get('daily-expenses')
  @ApiQuery({ name: 'storeCode', required: true })
  @ApiQuery({ name: 'businessDate', required: false })
  @ApiQuery({ name: 'from', required: false })
  @ApiQuery({ name: 'to', required: false })
  @ApiQuery({ name: 'status', required: false })
  @ApiQuery({ name: 'limit', required: false })
  async listDailyExpenses(
    @Query('storeCode') storeCode: string,
    @Query('businessDate') businessDate?: string,
    @Query('from') from?: string,
    @Query('to') to?: string,
    @Query('status') status?: string,
    @Query('limit') limit?: string,
  ) {
    const parsed = limit ? Number(limit) : 100;
    return await this.storePosQuery.listDailyExpenses(storeCode, {
      businessDate,
      from,
      to,
      status,
      limit: Number.isFinite(parsed) ? parsed : 100,
    });
  }

  @Post('next-number')
  async nextNumber(
    @Body()
    body: {
      storeId: string;
      deviceId: string;
      posCounter?: string;
      kind: string;
    },
  ) {
    const write = requireWriteBody(body);
    return await this.storePosQuery.nextNumber({
      storeId: write.storeId,
      deviceId: write.deviceId,
      posCounter: write.posCounter ?? '1',
      kind: write.kind,
    });
  }

  @Get('held-bills')
  @ApiQuery({ name: 'storeCode', required: true })
  @ApiQuery({ name: 'deviceId', required: false })
  async listHeldBills(@Query('storeCode') storeCode: string, @Query('deviceId') deviceId?: string) {
    return await this.storePosQuery.listHeldBills(storeCode, deviceId);
  }

  @Put('held-bills/:holdNo')
  async upsertHeldBill(
    @Param('holdNo') holdNo: string,
    @Body() body: { storeId: string; deviceId: string; payload: Record<string, unknown> },
  ) {
    const write = requireWriteBody(body);
    return await this.storePosQuery.upsertHeldBill(
      write.storeId,
      write.deviceId,
      holdNo,
      write.payload ?? {},
    );
  }

  @Delete('held-bills/:holdNo')
  @ApiQuery({ name: 'storeCode', required: true })
  async deleteHeldBill(@Param('holdNo') holdNo: string, @Query('storeCode') storeCode: string) {
    return await this.storePosQuery.deleteHeldBill(storeCode, holdNo);
  }

  @Post('events')
  async applyEvent(@Body() body: StorePosEventBody) {
    const write = requireWriteBody(body);
    if (!write.type?.trim()) {
      throw new BadRequestException('type is required');
    }
    return await this.storePos.applyEvent({
      type: write.type,
      storeId: write.storeId,
      deviceId: write.deviceId,
      payload: write.payload ?? {},
      ...(write.eventId ? { eventId: write.eventId } : {}),
      ...(write.hash ? { hash: write.hash } : {}),
      ...(write.createdAt ? { createdAt: write.createdAt } : {}),
    });
  }

  @Post('bills')
  async createBill(@Body() body: StorePosWriteBody) {
    const write = requireWriteBody(body);
    return await this.storePos.createBill(write.storeId, write.deviceId, write.payload ?? {});
  }

  @Delete('bills/:billNo')
  async deleteBill(@Param('billNo') billNo: string, @Body() body: StorePosWriteBody) {
    const write = requireWriteBody(body);
    return await this.storePos.deleteBill(write.storeId, write.deviceId, billNo, write.payload);
  }

  @Post('sale-returns')
  async createSaleReturn(@Body() body: StorePosWriteBody & { exchange?: boolean }) {
    const write = requireWriteBody(body);
    return await this.storePos.createSaleReturn(
      write.storeId,
      write.deviceId,
      write.payload ?? {},
      write.exchange === true,
    );
  }

  @Post('quotations')
  async upsertQuotation(@Body() body: StorePosWriteBody) {
    const write = requireWriteBody(body);
    return await this.storePos.upsertQuotation(write.storeId, write.deviceId, write.payload ?? {});
  }

  @Post('quotations/convert')
  async convertQuotation(@Body() body: StorePosWriteBody) {
    const write = requireWriteBody(body);
    return await this.storePos.convertQuotation(write.storeId, write.deviceId, write.payload ?? {});
  }

  @Post('quotations/cancel')
  async cancelQuotation(@Body() body: StorePosWriteBody) {
    const write = requireWriteBody(body);
    return await this.storePos.cancelQuotation(write.storeId, write.deviceId, write.payload ?? {});
  }

  @Post('credit-notes')
  async createCreditNote(@Body() body: StorePosWriteBody) {
    const write = requireWriteBody(body);
    return await this.storePos.createCreditNote(write.storeId, write.deviceId, write.payload ?? {});
  }

  @Post('credit-notes/apply')
  async applyCreditNote(@Body() body: StorePosWriteBody) {
    const write = requireWriteBody(body);
    return await this.storePos.applyCreditNote(write.storeId, write.deviceId, write.payload ?? {});
  }

  @Post('credit-notes/cashout')
  async cashoutCreditNote(@Body() body: StorePosWriteBody) {
    const write = requireWriteBody(body);
    return await this.storePos.cashoutCreditNote(write.storeId, write.deviceId, write.payload ?? {});
  }

  @Post('day-sessions/open')
  async openDaySession(@Body() body: StorePosWriteBody) {
    const write = requireWriteBody(body);
    return await this.storePos.openDaySession(write.storeId, write.deviceId, write.payload ?? {});
  }

  @Post('day-sessions/close')
  async closeDaySession(@Body() body: StorePosWriteBody) {
    const write = requireWriteBody(body);
    return await this.storePos.closeDaySession(write.storeId, write.deviceId, write.payload ?? {});
  }

  @Post('cash-movements')
  async createCashMovement(@Body() body: StorePosWriteBody) {
    const write = requireWriteBody(body);
    return await this.storePos.createCashMovement(write.storeId, write.deviceId, write.payload ?? {});
  }

  @Post('daily-expenses')
  async createDailyExpense(@Body() body: StorePosWriteBody) {
    const write = requireWriteBody(body);
    return await this.storePos.createDailyExpense(write.storeId, write.deviceId, write.payload ?? {});
  }

  @Put('daily-expenses/:expenseNo')
  async updateDailyExpense(
    @Param('expenseNo') expenseNo: string,
    @Body() body: StorePosWriteBody,
  ) {
    const write = requireWriteBody(body);
    return await this.storePos.updateDailyExpense(
      write.storeId,
      write.deviceId,
      expenseNo,
      write.payload ?? {},
    );
  }

  @Post('daily-expenses/:expenseNo/void')
  async voidDailyExpense(
    @Param('expenseNo') expenseNo: string,
    @Body() body: StorePosWriteBody,
  ) {
    const write = requireWriteBody(body);
    return await this.storePos.voidDailyExpense(
      write.storeId,
      write.deviceId,
      expenseNo,
      write.payload ?? {},
    );
  }

  /** Compatibility wrapper used by current WPF; persisted as the canonical dispatch charge event. */
  @Post('dispatch-charges')
  async createDispatchCharge(@Body() body: StorePosWriteBody) {
    const write = requireWriteBody(body);
    return this.storePos.applyEvent({
      type: 'OutboundDispatchChargeReceived',
      storeId: write.storeId,
      deviceId: write.deviceId,
      payload: write.payload ?? {},
      ...(write.eventId ? { eventId: write.eventId } : {}),
    });
  }

  /** Compatibility wrapper for WPF receipt reversal outbox records. */
  @Post('dispatch-charges/:receiptNo/void')
  async voidDispatchCharge(
    @Param('receiptNo') receiptNo: string,
    @Body() body: StorePosWriteBody,
  ) {
    const write = requireWriteBody(body);
    return this.storePos.applyEvent({
      type: 'DispatchChargeVoided',
      storeId: write.storeId,
      deviceId: write.deviceId,
      payload: { ...(write.payload ?? {}), receiptNo },
      ...(write.eventId ? { eventId: write.eventId } : {}),
    });
  }

  @Post('bills/:billNo/cod-payment')
  async receiveCodPayment(@Param('billNo') billNo: string, @Body() body: StorePosWriteBody) {
    const write = requireWriteBody(body);
    return await this.storePos.receiveCodPayment(write.storeId, write.deviceId, billNo, write.payload ?? {});
  }

  @Post('bills/:billNo/credit-payment')
  async receiveCreditPayment(@Param('billNo') billNo: string, @Body() body: StorePosWriteBody) {
    const write = requireWriteBody(body);
    return await this.storePos.receiveCreditPayment(
      write.storeId,
      write.deviceId,
      billNo,
      write.payload ?? {},
    );
  }

  @Post('adjustment-bills')
  async createAdjustmentBill(@Body() body: StorePosWriteBody) {
    const write = requireWriteBody(body);
    return await this.storePos.createAdjustmentBill(write.storeId, write.deviceId, write.payload ?? {});
  }

  @Get('payment-receipts/:receiptNo')
  @ApiQuery({ name: 'storeCode', required: true })
  async getPaymentReceipt(
    @Param('receiptNo') receiptNo: string,
    @Query('storeCode') storeCode: string,
  ) {
    return await this.storePosQuery.getPaymentReceipt(storeCode, receiptNo);
  }

  @Get('adjustment-bills')
  @ApiQuery({ name: 'storeCode', required: true })
  @ApiQuery({ name: 'originalBillNo', required: false })
  @ApiQuery({ name: 'limit', required: false })
  async listAdjustments(
    @Query('storeCode') storeCode: string,
    @Query('originalBillNo') originalBillNo?: string,
    @Query('limit') limit?: string,
  ) {
    const parsed = limit ? Number(limit) : 50;
    return await this.storePosQuery.listAdjustments(
      storeCode,
      originalBillNo,
      Number.isFinite(parsed) ? parsed : 50,
    );
  }

  @Get('adjustment-bills/by-original-bill')
  @ApiQuery({ name: 'storeCode', required: true })
  @ApiQuery({ name: 'originalBillNo', required: true })
  async getAdjustmentByOriginalBill(
    @Query('storeCode') storeCode: string,
    @Query('originalBillNo') originalBillNo: string,
  ) {
    return await this.storePosQuery.getAdjustmentByOriginalBill(storeCode, originalBillNo);
  }

  @Get('gateway-payments')
  @ApiQuery({ name: 'storeCode', required: true })
  @ApiQuery({ name: 'limit', required: false })
  @ApiQuery({ name: 'posCounter', required: false })
  async listGatewayPayments(
    @Query('storeCode') storeCode: string,
    @Query('limit') limit?: string,
    @Query('posCounter') posCounter?: string,
  ) {
    const parsed = limit ? Number(limit) : 100;
    return await this.storePosQuery.listGatewayPayments(
      storeCode,
      Number.isFinite(parsed) ? parsed : 100,
      posCounter,
    );
  }

  @Get('credit-note-cashouts')
  @ApiQuery({ name: 'storeCode', required: true })
  @ApiQuery({ name: 'businessDate', required: false })
  @ApiQuery({ name: 'limit', required: false })
  async listCreditNoteCashouts(
    @Query('storeCode') storeCode: string,
    @Query('businessDate') businessDate?: string,
    @Query('limit') limit?: string,
  ) {
    const parsed = limit ? Number(limit) : 100;
    return await this.storePosQuery.listCreditNoteCashouts(
      storeCode,
      businessDate,
      Number.isFinite(parsed) ? parsed : 100,
    );
  }

  @Get('promotions')
  @ApiQuery({ name: 'storeCode', required: true })
  async listPromotions(@Query('storeCode') storeCode: string) {
    return await this.storePosQuery.listActivePromotions(storeCode);
  }

  @Get('transfers/awaiting-intake')
  @ApiQuery({ name: 'storeId', required: true })
  @ApiQuery({ name: 'limit', required: false })
  async listAwaitingTransfers(
    @Query('storeId') storeId: string,
    @Query('limit') limit?: string,
  ) {
    const parsed = limit ? Number(limit) : 200;
    return await this.storePos.listAwaitingTransfers(
      storeId,
      Number.isFinite(parsed) ? parsed : 200,
    );
  }

  @Post('bills/:billNo/stock-exceptions/approve')
  async approveStockExceptions(@Param('billNo') billNo: string, @Body() body: StorePosWriteBody) {
    const write = requireWriteBody(body);
    return await this.storePos.approveStockExceptions(
      write.storeId,
      write.deviceId,
      billNo,
      write.payload ?? {},
    );
  }

  @Post('bills/:billNo/whatsapp')
  async updateBillWhatsApp(@Param('billNo') billNo: string, @Body() body: StorePosWriteBody) {
    const write = requireWriteBody(body);
    return await this.storePos.updateBillWhatsApp(
      write.storeId,
      write.deviceId,
      billNo,
      write.payload ?? {},
    );
  }

  @Post('bills/:billNo/print-audit')
  async appendBillPrintAudit(@Param('billNo') billNo: string, @Body() body: StorePosWriteBody) {
    const write = requireWriteBody(body);
    return await this.storePos.appendBillPrintAudit(
      write.storeId,
      write.deviceId,
      billNo,
      write.payload ?? {},
    );
  }

  @Post('day-sessions/cash-handover-printed')
  async markCashHandOverPrinted(@Body() body: StorePosWriteBody) {
    const write = requireWriteBody(body);
    return await this.storePos.markCashHandOverPrinted(
      write.storeId,
      write.deviceId,
      write.payload ?? {},
    );
  }

  @Post('gateway-payments')
  async recordGatewayPayment(@Body() body: StorePosWriteBody) {
    const write = requireWriteBody(body);
    return await this.storePos.recordGatewayPayment(write.storeId, write.deviceId, write.payload ?? {});
  }
}
