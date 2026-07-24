import {
  Body,
  Controller,
  Get,
  Param,
  Patch,
  Post,
  Query,
  Req,
  UseGuards,
} from '@nestjs/common';
import { ApiBearerAuth, ApiQuery, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { JwtPayload } from '../../common/jwt-payload';
import { CreateCustomerDto } from '../customers/dto/create-customer.dto';
import { UpdateCustomerDto } from '../customers/dto/update-customer.dto';
import { CreateSalesmanDto } from '../salesmen/dto/create-salesman.dto';
import { UpdateSalesmanDto } from '../salesmen/dto/update-salesman.dto';
import { PosV2Service } from './pos-v2.service';

@ApiTags('pos-v2')
@Controller('pos-v2')
export class PosV2Controller {
  constructor(private readonly posV2: PosV2Service) {}

  @Get('health')
  health() {
    return this.posV2.health();
  }

  @Post('auth/login')
  login(
    @Body()
    body: {
      email: string;
      password: string;
      storeId?: string;
      deviceId?: string;
      posCounter?: string;
    },
  ) {
    return this.posV2.login(body);
  }

  @Post('auth/logout')
  logout() {
    return { ok: true };
  }

  @Get('session')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  session(@Req() req: { user: JwtPayload }) {
    return this.posV2.getSession(req.user);
  }

  @Get('company-billing-settings')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  @ApiQuery({ name: 'storeId', required: true })
  getBillingSettings(@Query('storeId') storeId: string) {
    return this.posV2.getBillingSettings(storeId);
  }

  @Patch('company-billing-settings')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  @ApiQuery({ name: 'storeId', required: true })
  patchBillingSettings(
    @Query('storeId') storeId: string,
    @Body() body: { mode?: string; allowPerBillSwitch?: boolean },
  ) {
    return this.posV2.patchBillingSettings(storeId, body);
  }

  @Get('catalog')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  @ApiQuery({ name: 'storeId', required: true })
  getCatalog(
    @Query('storeId') storeId: string,
    @Query('search') search?: string,
    @Query('category') category?: string,
    @Query('limit') limit?: string,
  ) {
    const params: {
      storeId: string;
      search?: string;
      category?: string;
      limit?: number;
    } = { storeId };
    if (search !== undefined) params.search = search;
    if (category !== undefined) params.category = category;
    if (limit !== undefined) params.limit = Number(limit);
    return this.posV2.getCatalog(params);
  }

  @Get('inventory')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  getInventory(
    @Query('storeId') storeId: string,
    @Query('search') search?: string,
    @Query('category') category?: string,
    @Query('brand') brand?: string,
    @Query('stockLevel') stockLevel?: string,
    @Query('page') page?: string,
    @Query('limit') limit?: string,
  ) {
    const params: {
      storeId: string;
      search?: string;
      category?: string;
      brand?: string;
      stockLevel?: string;
      page?: number;
      limit?: number;
    } = { storeId };
    if (search !== undefined) params.search = search;
    if (category !== undefined) params.category = category;
    if (brand !== undefined) params.brand = brand;
    if (stockLevel !== undefined) params.stockLevel = stockLevel;
    if (page !== undefined) params.page = Number(page);
    if (limit !== undefined) params.limit = Number(limit);
    return this.posV2.getInventory(params);
  }

  @Get('masters')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  masters(@Query('storeId') storeId: string) {
    return this.posV2.getMasters(storeId);
  }

  @Post('bills')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  createBill(@Req() req: { user: JwtPayload }, @Body() body: Record<string, unknown>) {
    return this.posV2.createBill(req.user, body);
  }

  @Get('bills')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  listBills(
    @Query('storeCode') storeCode: string,
    @Query('search') search?: string,
    @Query('from') from?: string,
    @Query('to') to?: string,
    @Query('page') page?: string,
    @Query('limit') limit?: string,
    @Query('status') status?: string,
    @Query('paymentMode') paymentMode?: string,
    @Query('salesmanCode') salesmanCode?: string,
  ) {
    const params: {
      storeCode: string;
      search?: string;
      from?: string;
      to?: string;
      page?: number;
      limit?: number;
      status?: string;
      paymentMode?: string;
      salesmanCode?: string;
    } = { storeCode };
    if (search !== undefined) params.search = search;
    if (from !== undefined) params.from = from;
    if (to !== undefined) params.to = to;
    if (page !== undefined) params.page = Number(page);
    if (limit !== undefined) params.limit = Number(limit);
    if (status !== undefined) params.status = status;
    if (paymentMode !== undefined) params.paymentMode = paymentMode;
    if (salesmanCode !== undefined) params.salesmanCode = salesmanCode;
    return this.posV2.listBills(params);
  }

  @Get('bills/:billNo')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  getBill(@Query('storeCode') storeCode: string, @Param('billNo') billNo: string) {
    return this.posV2.getBill(storeCode, billNo);
  }

  @Get('history/bills')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  historyBills(
    @Query('storeCode') storeCode: string,
    @Query('search') search?: string,
    @Query('page') page?: string,
    @Query('limit') limit?: string,
  ) {
    const params: {
      storeCode: string;
      search?: string;
      page?: number;
      limit?: number;
    } = { storeCode };
    if (search !== undefined) params.search = search;
    if (page !== undefined) params.page = Number(page);
    if (limit !== undefined) params.limit = Number(limit);
    return this.posV2.listBills(params);
  }

  @Post('returns')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  createReturn(@Req() req: { user: JwtPayload }, @Body() body: Record<string, unknown>) {
    const kind = body.kind === 'exchange' ? 'exchange' : 'return';
    return this.posV2.createReturn(req.user, body, kind);
  }

  @Get('returns')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  listReturns(@Query('storeId') storeId: string) {
    return this.posV2.listReturns(storeId);
  }

  @Post('adjustments')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  createAdjustment(@Req() req: { user: JwtPayload }, @Body() body: Record<string, unknown>) {
    return this.posV2.createAdjustment(req.user, body);
  }

  @Post('quotations')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  upsertQuotation(@Req() req: { user: JwtPayload }, @Body() body: Record<string, unknown>) {
    return this.posV2.upsertQuotation(req.user, body);
  }

  @Get('quotations')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  listQuotations(@Query('storeId') storeId: string, @Query('search') search?: string) {
    return this.posV2.listQuotations(storeId, search);
  }

  @Post('quotations/convert')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  convertQuotation(@Req() req: { user: JwtPayload }, @Body() body: Record<string, unknown>) {
    return this.posV2.convertQuotation(req.user, body);
  }

  @Post('quotations/cancel')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  cancelQuotation(@Req() req: { user: JwtPayload }, @Body() body: Record<string, unknown>) {
    return this.posV2.cancelQuotation(req.user, body);
  }

  @Post('credit-notes')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  createCreditNote(@Req() req: { user: JwtPayload }, @Body() body: Record<string, unknown>) {
    return this.posV2.createCreditNote(req.user, body);
  }

  @Post('credit-notes/apply')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  applyCreditNote(@Req() req: { user: JwtPayload }, @Body() body: Record<string, unknown>) {
    return this.posV2.applyCreditNote(req.user, body);
  }

  @Post('credit-notes/cashout')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  cashoutCreditNote(@Req() req: { user: JwtPayload }, @Body() body: Record<string, unknown>) {
    return this.posV2.cashoutCreditNote(req.user, body);
  }

  @Get('credit-notes')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  listCreditNotes(@Query('storeId') storeId: string) {
    return this.posV2.listCreditNotes(storeId);
  }

  @Post('payments/cod')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  recordCod(@Req() req: { user: JwtPayload }, @Body() body: Record<string, unknown>) {
    return this.posV2.recordCodPayment(req.user, body);
  }

  @Post('payments/credit')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  recordCredit(@Req() req: { user: JwtPayload }, @Body() body: Record<string, unknown>) {
    return this.posV2.recordCreditPayment(req.user, body);
  }

  @Post('day-sessions/open')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  openDay(@Req() req: { user: JwtPayload }, @Body() body: Record<string, unknown>) {
    return this.posV2.openDaySession(req.user, body);
  }

  @Post('day-sessions/close')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  closeDay(@Req() req: { user: JwtPayload }, @Body() body: Record<string, unknown>) {
    return this.posV2.closeDaySession(req.user, body);
  }

  @Get('day-sessions')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  listDaySessions(
    @Query('storeId') storeId: string,
    @Query('from') from?: string,
    @Query('to') to?: string,
  ) {
    return this.posV2.listDaySessions(storeId, from, to);
  }

  @Post('cash-movements')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  cashMovement(@Req() req: { user: JwtPayload }, @Body() body: Record<string, unknown>) {
    return this.posV2.createCashMovement(req.user, body);
  }

  @Get('cash-movements')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  listCashMovements(@Query('storeId') storeId: string) {
    return this.posV2.listCashMovements(storeId);
  }

  @Post('expenses')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  createExpense(@Req() req: { user: JwtPayload }, @Body() body: Record<string, unknown>) {
    return this.posV2.createExpense(req.user, body);
  }

  @Get('expenses')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  listExpenses(@Query('storeId') storeId: string) {
    return this.posV2.listExpenses(storeId);
  }

  @Get('online-sales')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  onlineSales(@Query('storeId') storeId: string) {
    return this.posV2.listOnlineSales(storeId);
  }

  @Get('credit-bills')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  creditBills(@Query('storeId') storeId: string, @Query('status') status?: string) {
    return this.posV2.listCreditBills(storeId, status);
  }

  @Get('dashboard')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  dashboard(@Query('storeId') storeId: string) {
    return this.posV2.dashboardOverview(storeId);
  }

  @Get('analytics')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  analytics(@Query('storeId') storeId: string) {
    return this.posV2.analytics(storeId);
  }

  @Get('customers')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  listCustomers(@Query('search') search?: string, @Query('phone') phone?: string) {
    return this.posV2.listCustomers(search, phone);
  }

  @Post('customers')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  createCustomer(@Body() body: CreateCustomerDto) {
    return this.posV2.createCustomer(body);
  }

  @Patch('customers/:id')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  updateCustomer(@Param('id') id: string, @Body() body: UpdateCustomerDto) {
    return this.posV2.updateCustomer(id, body);
  }

  @Get('salesmen')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  listSalesmen(@Query('storeId') storeId: string, @Query('search') search?: string) {
    return this.posV2.listSalesmen(storeId, search);
  }

  @Post('salesmen')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  createSalesman(@Body() body: CreateSalesmanDto) {
    return this.posV2.createSalesman(body);
  }

  @Patch('salesmen/:id')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  updateSalesman(@Param('id') id: string, @Body() body: UpdateSalesmanDto) {
    return this.posV2.updateSalesman(id, body);
  }

  @Post('inventory-adjustments')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  inventoryAdjustment(@Body() body: Record<string, unknown>) {
    return this.posV2.createInventoryAdjustment(body);
  }

  @Get('barcode-label-design')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  barcodeDesign() {
    return this.posV2.getActiveBarcodeDesign();
  }

  @Get('whatsapp/settings')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  whatsappSettings(@Query('storeId') storeId?: string) {
    return this.posV2.getWhatsAppSettings(storeId);
  }

  @Post('whatsapp/send-invoice')
  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard)
  sendWhatsApp(
    @Body()
    body: {
      storeId: string;
      billNo: string;
      phone: string;
      customerName?: string;
      payable?: number;
      attachmentBase64?: string;
      attachmentFilename?: string;
    },
  ) {
    return this.posV2.sendWhatsAppInvoice(body);
  }
}
