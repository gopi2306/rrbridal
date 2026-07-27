import { Body, Controller, Delete, Get, Param, Post, Put, Query } from '@nestjs/common';
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
  async listBills(
    @Query('storeCode') storeCode: string,
    @Query('search') search?: string,
    @Query('limit') limit?: string,
  ) {
    const parsed = limit ? Number(limit) : 50;
    return await this.storePosQuery.listBills(storeCode, search, Number.isFinite(parsed) ? parsed : 50);
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
  @ApiQuery({ name: 'businessDate', required: true })
  @ApiQuery({ name: 'limit', required: false })
  async listDailyExpenses(
    @Query('storeCode') storeCode: string,
    @Query('businessDate') businessDate: string,
    @Query('limit') limit?: string,
  ) {
    const parsed = limit ? Number(limit) : 100;
    return await this.storePosQuery.listDailyExpenses(
      storeCode,
      businessDate,
      Number.isFinite(parsed) ? parsed : 100,
    );
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
    return await this.storePosQuery.nextNumber({
      storeId: body.storeId,
      deviceId: body.deviceId,
      posCounter: body.posCounter ?? '1',
      kind: body.kind,
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
    return await this.storePosQuery.upsertHeldBill(
      body.storeId,
      body.deviceId,
      holdNo,
      body.payload ?? {},
    );
  }

  @Delete('held-bills/:holdNo')
  @ApiQuery({ name: 'storeCode', required: true })
  async deleteHeldBill(@Param('holdNo') holdNo: string, @Query('storeCode') storeCode: string) {
    return await this.storePosQuery.deleteHeldBill(storeCode, holdNo);
  }

  @Post('events')
  async applyEvent(@Body() body: StorePosEventBody) {
    return await this.storePos.applyEvent({
      type: body.type,
      storeId: body.storeId,
      deviceId: body.deviceId,
      payload: body.payload ?? {},
      ...(body.eventId ? { eventId: body.eventId } : {}),
      ...(body.hash ? { hash: body.hash } : {}),
      ...(body.createdAt ? { createdAt: body.createdAt } : {}),
    });
  }

  @Post('bills')
  async createBill(@Body() body: StorePosWriteBody) {
    return await this.storePos.createBill(body.storeId, body.deviceId, body.payload ?? {});
  }

  @Delete('bills/:billNo')
  async deleteBill(@Param('billNo') billNo: string, @Body() body: StorePosWriteBody) {
    return await this.storePos.deleteBill(body.storeId, body.deviceId, billNo, body.payload);
  }

  @Post('sale-returns')
  async createSaleReturn(@Body() body: StorePosWriteBody & { exchange?: boolean }) {
    return await this.storePos.createSaleReturn(
      body.storeId,
      body.deviceId,
      body.payload ?? {},
      body.exchange === true,
    );
  }

  @Post('quotations')
  async upsertQuotation(@Body() body: StorePosWriteBody) {
    return await this.storePos.upsertQuotation(body.storeId, body.deviceId, body.payload ?? {});
  }

  @Post('quotations/convert')
  async convertQuotation(@Body() body: StorePosWriteBody) {
    return await this.storePos.convertQuotation(body.storeId, body.deviceId, body.payload ?? {});
  }

  @Post('quotations/cancel')
  async cancelQuotation(@Body() body: StorePosWriteBody) {
    return await this.storePos.cancelQuotation(body.storeId, body.deviceId, body.payload ?? {});
  }

  @Post('credit-notes')
  async createCreditNote(@Body() body: StorePosWriteBody) {
    return await this.storePos.createCreditNote(body.storeId, body.deviceId, body.payload ?? {});
  }

  @Post('credit-notes/apply')
  async applyCreditNote(@Body() body: StorePosWriteBody) {
    return await this.storePos.applyCreditNote(body.storeId, body.deviceId, body.payload ?? {});
  }

  @Post('credit-notes/cashout')
  async cashoutCreditNote(@Body() body: StorePosWriteBody) {
    return await this.storePos.cashoutCreditNote(body.storeId, body.deviceId, body.payload ?? {});
  }

  @Post('day-sessions/open')
  async openDaySession(@Body() body: StorePosWriteBody) {
    return await this.storePos.openDaySession(body.storeId, body.deviceId, body.payload ?? {});
  }

  @Post('day-sessions/close')
  async closeDaySession(@Body() body: StorePosWriteBody) {
    return await this.storePos.closeDaySession(body.storeId, body.deviceId, body.payload ?? {});
  }

  @Post('cash-movements')
  async createCashMovement(@Body() body: StorePosWriteBody) {
    return await this.storePos.createCashMovement(body.storeId, body.deviceId, body.payload ?? {});
  }

  @Post('daily-expenses')
  async createDailyExpense(@Body() body: StorePosWriteBody) {
    return await this.storePos.createDailyExpense(body.storeId, body.deviceId, body.payload ?? {});
  }

  @Post('bills/:billNo/cod-payment')
  async receiveCodPayment(@Param('billNo') billNo: string, @Body() body: StorePosWriteBody) {
    return await this.storePos.receiveCodPayment(body.storeId, body.deviceId, billNo, body.payload ?? {});
  }

  @Post('bills/:billNo/credit-payment')
  async receiveCreditPayment(@Param('billNo') billNo: string, @Body() body: StorePosWriteBody) {
    return await this.storePos.receiveCreditPayment(
      body.storeId,
      body.deviceId,
      billNo,
      body.payload ?? {},
    );
  }

  @Post('adjustment-bills')
  async createAdjustmentBill(@Body() body: StorePosWriteBody) {
    return await this.storePos.createAdjustmentBill(body.storeId, body.deviceId, body.payload ?? {});
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

  @Post('bills/:billNo/stock-exceptions/approve')
  async approveStockExceptions(@Param('billNo') billNo: string, @Body() body: StorePosWriteBody) {
    return await this.storePos.approveStockExceptions(
      body.storeId,
      body.deviceId,
      billNo,
      body.payload ?? {},
    );
  }

  @Post('bills/:billNo/whatsapp')
  async updateBillWhatsApp(@Param('billNo') billNo: string, @Body() body: StorePosWriteBody) {
    return await this.storePos.updateBillWhatsApp(body.storeId, body.deviceId, billNo, body.payload ?? {});
  }

  @Post('bills/:billNo/print-audit')
  async appendBillPrintAudit(@Param('billNo') billNo: string, @Body() body: StorePosWriteBody) {
    return await this.storePos.appendBillPrintAudit(
      body.storeId,
      body.deviceId,
      billNo,
      body.payload ?? {},
    );
  }

  @Post('day-sessions/cash-handover-printed')
  async markCashHandOverPrinted(@Body() body: StorePosWriteBody) {
    return await this.storePos.markCashHandOverPrinted(body.storeId, body.deviceId, body.payload ?? {});
  }

  @Post('gateway-payments')
  async recordGatewayPayment(@Body() body: StorePosWriteBody) {
    return await this.storePos.recordGatewayPayment(body.storeId, body.deviceId, body.payload ?? {});
  }
}
